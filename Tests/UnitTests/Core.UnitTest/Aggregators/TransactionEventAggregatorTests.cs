using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.Aggregators
{
	[TestFixture]
	public class TransactionEventAggregatorTests
	{
		[NotNull]
		private IDataTransportService _dataTransportService;

		[NotNull]
		private IAgentHealthReporter _agentHealthReporter;

		[NotNull]
		private TransactionEventAggregator _transactionEventAggregator;

		[NotNull]
		private IProcessStatic _processStatic;

		[NotNull]
		private ConfigurationAutoResponder _configurationAutoResponder;

		[NotNull]
		private Action _harvestAction;

		private const string TimeStampKey = "timestamp";
		private readonly static Dictionary<string, object> _emptyAttributes = new Dictionary<string, object>();
		private readonly static Dictionary<string, object> _intrinsicAttributes = new Dictionary<string, object> { { TimeStampKey, DateTime.UtcNow.ToUnixTime() } };

		[SetUp]
		public void SetUp()
		{
			var configuration = GetDefaultConfiguration();
			Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
			Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
			_configurationAutoResponder = new ConfigurationAutoResponder(configuration);

			_dataTransportService = Mock.Create<IDataTransportService>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_processStatic = Mock.Create<IProcessStatic>();

			var scheduler = Mock.Create<IScheduler>();
			Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
				.DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestAction = action);
			_transactionEventAggregator = new TransactionEventAggregator(_dataTransportService, scheduler, _processStatic, _agentHealthReporter);
		}

		[TearDown]
		public void TearDown()
		{
			_transactionEventAggregator.Dispose();
			_configurationAutoResponder.Dispose();
		}

		#region Configuration

		[Test]
		public void Collections_are_reset_on_configuration_update_event()
		{
			// Arrange
			var configuration = GetDefaultConfiguration(int.MaxValue);
			var sentEvents = null as IEnumerable<TransactionEventWireModel>;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.DoInstead<IEnumerable<TransactionEventWireModel>>(events => sentEvents = events);
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>());

			// Act
			EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
			_harvestAction();

			// Assert
			Assert.Null(sentEvents);
		}

		#endregion

		[Test]
		public void Events_send_on_harvest()
		{
			// Arrange
			var sentEvents = null as IEnumerable<TransactionEventWireModel>;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.DoInstead<IEnumerable<TransactionEventWireModel>>(events => sentEvents = events);

			var eventsToSend = new[]
			{
				Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.3f),
				Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f),
				Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f)
			};
			eventsToSend.ForEach(_transactionEventAggregator.Collect);

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(3, sentEvents.Count());
			Assert.AreEqual(sentEvents, eventsToSend);
		}

		[Test]
		public void Event_seen_reported_on_collect()
		{
			// Act
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected());
		}

		[Test]
		public void Events_sent_reported_on_harvest()
		{
			// Arrange
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));

			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected());
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsSent(1));
		}

		[Test]
		public void Reservoir_resized_reported_on_post_too_big_response()
		{
			// Arrange
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					return DataTransportResponseStatus.PostTooBigError;
				});

			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventReservoirResized(1));
		}

		[Test]
		public void Events_are_not_sent_if_there_are_no_events_to_send()
		{
			// Arrange
			var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sendCalled = true;
					return DataTransportResponseStatus.RequestSuccessful;
				});

			// Act
			_harvestAction();

			// Assert
			Assert.False(sendCalled);
		}

		[Test]
		public void Events_are_not_retained_after_harvest_if_response_equals_request_successful()
		{
			// Arrange
			IEnumerable<TransactionEventWireModel> sentEvents = null;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEvents = events;
					return DataTransportResponseStatus.RequestSuccessful;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEvents = null; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.Null(sentEvents);
		}

		[Test]
		public void Events_are_not_retained_after_harvest_if_response_equals_unknown_error()
		{
			// Arrange
			IEnumerable<TransactionEventWireModel> sentEvents = null;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEvents = events;
					return DataTransportResponseStatus.OtherError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEvents = null; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.Null(sentEvents);
		}

		[Test]
		public void Events_are_retained_after_harvest_if_response_equals_connection_error()
		{
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.ConnectionError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(2, sentEventCount);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
		}

		[Test]
		public void Events_are_retained_after_harvest_if_response_equals_service_unavailable_error()
		{
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.ServerError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(2, sentEventCount);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
		}

		[Test]
		public void Events_are_retained_after_harvest_if_response_equals_communication_error()
		{
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.CommunicationError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(2, sentEventCount);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
		}

		[Test]
		public void Events_are_retained_after_harvest_if_response_equals_request_timeout_error()
		{
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.RequestTimeout;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(2, sentEventCount);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
		}

		[Test]
		public void Half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
		{
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.PostTooBigError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.AreEqual(1, sentEventCount);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
		}

		[Test]
		public void Zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
		{
			// Arrange
			IEnumerable<TransactionEventWireModel> sentEvents = null;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
				.Returns<IEnumerable<TransactionEventWireModel>>(events =>
				{
					sentEvents = events;
					return DataTransportResponseStatus.PostTooBigError;
				});
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>());
			_harvestAction();
			sentEvents = null; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.Null(sentEvents);
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(1));
		}

		[Test]
		public void When_no_events_are_published_then_no_events_are_reported_to_agent_health()
		{
			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected(), Occurs.Never());
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(Arg.IsAny<Int32>()), Occurs.Never());
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventReservoirResized(Arg.IsAny<UInt32>()), Occurs.Never());
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsSent(Arg.IsAny<Int32>()), Occurs.Never());
		}

		[Test]
		public void When_event_is_collected_then_events_seen_is_reported_to_agent_health()
		{
			// Act
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected());
		}

		[Test]
		public void When_harvesting_events_then_event_sent_is_reported_to_agent_health()
		{
			// Arrange
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.2f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.1f));
			_transactionEventAggregator.Collect(Mock.Create<TransactionEventWireModel>(_emptyAttributes, _emptyAttributes, _intrinsicAttributes, false, 0.3f));

			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsSent(3));
		}

		#region Helpers

		[NotNull]
		private static IConfiguration GetDefaultConfiguration(int? versionNumber=null)
		{
			var configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
			Mock.Arrange(() => configuration.TransactionEventsMaxSamplesStored).Returns(10000);
			Mock.Arrange(() => configuration.TransactionEventsTransactionsEnabled).Returns(true);
			Mock.Arrange(() => configuration.CaptureTransactionEventsAttributes).Returns(true);
			if (versionNumber.HasValue)   
				Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
			return configuration;
		}

		#endregion
	}
}

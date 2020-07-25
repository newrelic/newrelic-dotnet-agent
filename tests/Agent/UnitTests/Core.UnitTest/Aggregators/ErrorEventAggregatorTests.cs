using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class ErrorEventAggregatorTests
    {
        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private ErrorEventAggregator _errorEventAggregator;
        private IProcessStatic _processStatic;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private Action _harvestAction;

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
            _errorEventAggregator = new ErrorEventAggregator(_dataTransportService, scheduler, _processStatic, _agentHealthReporter);
        }

        [TearDown]
        public void TearDown()
        {
            _errorEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        #region Configuration

        [Test]
        public void collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentEvents = null as IEnumerable<ErrorEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .DoInstead<IEnumerable<ErrorEventWireModel>>(events => sentEvents = events);
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Act
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        #endregion

        [Test]
        public void events_send_on_harvest()
        {
            // Arrange
            var sentEvents = null as IEnumerable<ErrorEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .DoInstead<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) => sentEvents = events);

            var eventsToSend = new[]
            {
                Mock.Create<ErrorEventWireModel>(),
                Mock.Create<ErrorEventWireModel>(),
                Mock.Create<ErrorEventWireModel>()
            };
            eventsToSend.ForEach(_errorEventAggregator.Collect);

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(3, sentEvents.Count());
            Assert.AreEqual(sentEvents, eventsToSend);
        }

        [Test]
        public void event_seen_reported_on_collect()
        {
            // Act
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
        }

        [Test]
        public void events_sent_reported_on_harvest()
        {
            // Arrange
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(1));
        }

        [Test]
        public void events_are_not_sent_if_there_are_no_events_to_send()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<IEnumerable<ErrorEventWireModel>>(events =>
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
        public void events_are_not_retained_after_harvest_if_response_equals_request_successful()
        {
            // Arrange
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.RequestSuccessful;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEvents = null; // reset

            // Act- re-run Harvest to verify the Events are no longer there
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void events_are_not_retained_after_harvest_if_response_equals_unknown_error()
        {
            // Arrange
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.OtherError;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void events_are_retained_after_harvest_if_response_equals_connection_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ConnectionError;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act - re-run Harvest to verify the Events are still there
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
        }

        [Test]
        public void events_are_retained_after_harvest_if_response_equals_service_unavailable_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ServiceUnavailableError;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
        }

        [Test]
        public void half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.PostTooBigError;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentEventCount);
        }

        [Test]
        public void zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
        {
            // Arrange
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<ErrorEventAdditions, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.PostTooBigError;
                });
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void when_no_events_are_published_then_no_events_are_reported_to_agent_health()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(Arg.IsAny<Int32>()), Occurs.Never());
        }

        [Test]
        public void when_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
        }

        [Test]
        public void when_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(3));
        }

        [Test]
        public void when_more_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 105;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var errorEvents = privateAccessor.GetField("_errorEvents") as IResizableCappedCollection<ErrorEventWireModel>;
            if (errorEvents == null) throw new ArgumentNullException(nameof(errorEvents));
            var actualAddAttempts = errorEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void when_less_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 99;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var errorEvents = privateAccessor.GetField("_errorEvents") as IResizableCappedCollection<ErrorEventWireModel>;
            if (errorEvents == null) throw new ArgumentNullException(nameof(errorEvents));
            var actualAddAttempts = errorEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void when_harvest_occurs_default_reservoir_size_is_reported_accurately()
        {
            const uint expectedReservoirSize = 100;

            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());
            _errorEventAggregator.Collect(Mock.Create<ErrorEventWireModel>());

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var actualReservoirSize = privateAccessor.CallMethod("GetReservoirSize");

            _harvestAction();

            Assert.AreEqual(expectedReservoirSize, actualReservoirSize);
        }

        #region Helpers
        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.ErrorCollectorMaxEventSamplesStored).Returns(100);
            Mock.Arrange(() => configuration.ErrorCollectorCaptureEvents).Returns(true);
            Mock.Arrange(() => configuration.CaptureErrorCollectorAttributes).Returns(true);
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

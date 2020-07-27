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
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class CustomEventAggregatorTests
    {
        [NotNull]
        private IDataTransportService _dataTransportService;

        [NotNull]
        private IAgentHealthReporter _agentHealthReporter;

        [NotNull]
        private CustomEventAggregator _customEventAggregator;

        [NotNull]
        private IProcessStatic _processStatic;

        [NotNull]
        private ConfigurationAutoResponder _configurationAutoResponder;

        [NotNull]
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
            _customEventAggregator = new CustomEventAggregator(_dataTransportService, scheduler, _processStatic, _agentHealthReporter);
        }

        [TearDown]
        public void TearDown()
        {
            _customEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        #region Configuration

        [Test]
        public void collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentEvents = null as IEnumerable<CustomEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .DoInstead<IEnumerable<CustomEventWireModel>>(events => sentEvents = events);
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

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
            var sentEvents = null as IEnumerable<CustomEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .DoInstead<IEnumerable<CustomEventWireModel>>(events => sentEvents = events);

            var eventsToSend = new[]
            {
                Mock.Create<CustomEventWireModel>(),
                Mock.Create<CustomEventWireModel>(),
                Mock.Create<CustomEventWireModel>()
            };
            eventsToSend.ForEach(_customEventAggregator.Collect);

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
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
        }

        [Test]
        public void events_sent_reported_on_harvest()
        {
            // Arrange
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(1));
        }

        [Test]
        public void reservoir_resized_reported_on_post_too_big_response()
        {
            // Arrange
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    return DataTransportResponseStatus.PostTooBigError;
                });

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventReservoirResized(1));
        }

        [Test]
        public void events_are_not_sent_if_there_are_no_events_to_send()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
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
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.RequestSuccessful;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void events_are_not_retained_after_harvest_if_response_equals_unknown_error()
        {
            // Arrange
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.OtherError;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
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
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ConnectionError;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(2));
        }

        [Test]
        public void events_are_retained_after_harvest_if_response_equals_service_unavailable_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ServiceUnavailableError;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(2));
        }

        [Test]
        public void half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.PostTooBigError;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentEventCount);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(2));
        }

        [Test]
        public void zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
        {
            // Arrange
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.PostTooBigError;
                });
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(1));
        }

        [Test]
        public void when_no_events_are_published_then_no_events_are_reported_to_agent_health()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(Arg.IsAny<Int32>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventReservoirResized(Arg.IsAny<UInt32>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(Arg.IsAny<Int32>()), Occurs.Never());
        }

        [Test]
        public void when_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
        }

        [Test]
        public void when_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(3));
        }

        #region Helpers

        [NotNull]
        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.CustomEventsMaxSamplesStored).Returns(10000);
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.Core;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private static readonly TimeSpan ConfiguredHarvestCycle = TimeSpan.FromSeconds(5);

        private const string TimeStampKey = "timestamp";
        private readonly static Dictionary<string, object> _emptyAttributes = new Dictionary<string, object>();
        private readonly static Dictionary<string, object> _intrinsicAttributes = new Dictionary<string, object> { { TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds() } };

        private readonly static AttributeValueCollection _attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.ErrorEventsHarvestCycle).Returns(ConfiguredHarvestCycle);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _processStatic = Mock.Create<IProcessStatic>();

            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _errorEventAggregator = new ErrorEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _errorEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        #region Configuration

        [Test]
        public void Collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentEvents = null as IEnumerable<ErrorEventWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .DoInstead<IEnumerable<ErrorEventWireModel>>(events => sentEvents = events);
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));

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
            var sentEvents = null as IEnumerable<ErrorEventWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .DoInstead<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) => sentEvents = events);

            var eventsToSend = new[]
            {
                new ErrorEventWireModel(_attribValues, false, 0.3f),
                new ErrorEventWireModel(_attribValues, false, 0.2f),
                new ErrorEventWireModel(_attribValues, false, 0.1f)
            };
            eventsToSend.ForEach(_errorEventAggregator.Collect);

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
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
        }

        [Test]
        public void Events_sent_reported_on_harvest()
        {

            // Arrange
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(1));
        }

        [Test]
        public void Events_are_not_sent_if_there_are_no_events_to_send()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<IEnumerable<ErrorEventWireModel>>(events =>
                {
                    sendCalled = true;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
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
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });

            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEvents = null; // reset

            // Act- re-run Harvest to verify the Events are no longer there
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void Events_are_not_retained_after_harvest_if_response_equals_discard()
        {
            // Arrange
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.Discard);
                });
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void Events_are_retained_after_harvest_if_response_equals_retain()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act - re-run Harvest to verify the Events are still there
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
        }

        [Test]
        public void Half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentEventCount);
        }

        [Test]
        public void Zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
        {
            // Arrange
            IEnumerable<ErrorEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns<EventHarvestData, IEnumerable<ErrorEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void When_no_events_are_published_then_no_events_are_reported_to_agent_health()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(Arg.IsAny<int>()), Occurs.Never());
        }

        [Test]
        public void When_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventSeen());
        }

        [Test]
        public void When_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.3f));

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorEventsSent(3));
        }

        [Test]
        public void When_more_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 105;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, i * 0.001f));
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var errorEvents = privateAccessor.GetField("_errorEvents") as ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>>;
            if (errorEvents == null) throw new ArgumentNullException(nameof(errorEvents));
            var actualAddAttempts = errorEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void When_less_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 99;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, i * 0.001f));
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var errorEvents = privateAccessor.GetField("_errorEvents") as ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>>;
            if (errorEvents == null) throw new ArgumentNullException(nameof(errorEvents));
            var actualAddAttempts = errorEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void When_harvest_occurs_default_reservoir_size_is_reported_accurately()
        {
            const uint expectedReservoirSize = 100;

            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.1f));
            _errorEventAggregator.Collect(new ErrorEventWireModel(_attribValues, false, 0.2f));

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_errorEventAggregator);
            var actualReservoirSize = privateAccessor.CallMethod("GetReservoirSize");

            _harvestAction();

            Assert.AreEqual(expectedReservoirSize, actualReservoirSize);
        }

        [Test]
        public void When_error_events_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _errorEventAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ErrorCollectorCaptureEvents).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _errorEventAggregator = new ErrorEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }

        [Test]
        public void Harvest_cycle_should_match_configured_cycle()
        {
            Assert.AreEqual(ConfiguredHarvestCycle, _harvestCycle);
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

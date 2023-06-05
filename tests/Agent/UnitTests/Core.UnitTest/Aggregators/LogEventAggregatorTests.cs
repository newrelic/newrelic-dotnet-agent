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
    public class LogEventAggregatorTests
    {
        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private LogEventAggregator _logEventAggregator;
        private IProcessStatic _processStatic;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private static readonly TimeSpan ConfiguredHarvestCycle = TimeSpan.FromSeconds(5);

        private const string TimeStampKey = "timestamp";

        private Dictionary<string, object> _contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.LogEventsHarvestCycle).Returns(ConfiguredHarvestCycle);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _processStatic = Mock.Create<IProcessStatic>();

            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _logEventAggregator = new LogEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _logEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        #region Configuration

        [Test]
        public void Collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentEvents = null as LogEventWireModelCollection;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .DoInstead<LogEventWireModelCollection>(events => sentEvents = events);
            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };

            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

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
            var sentEvents = null as LogEventWireModelCollection;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .DoInstead<LogEventWireModelCollection>(events => sentEvents = events);

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(2, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(3, "message1", "info", "spanid", "traceid", _contextData)
            };

            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(3, sentEvents.LoggingEvents.Count());
            Assert.AreEqual(sentEvents.LoggingEvents, logEvents);
        }

        [Test]
        public void Event_seen_reported_on_collect()
        {
            // Act
            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventCollected());
        }

        [Test]
        public void Events_sent_reported_on_harvest()
        {

            // Arrange
            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventCollected());
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventsSent(1));
        }

        [Test]
        public void Events_are_not_sent_if_there_are_no_events_to_send()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
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
            LogEventWireModelCollection sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);
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
            LogEventWireModelCollection sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.Discard);
                });

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);
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
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
                {
                    sentEventCount = events.LoggingEvents.Count();
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(2, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);
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
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
                {
                    sentEventCount = events.LoggingEvents.Count();
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(2, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);
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
            LogEventWireModelCollection sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .Returns<LogEventWireModelCollection>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);
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
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventCollected(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventsSent(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventsDropped(Arg.IsAny<int>()), Occurs.Never());
        }

        [Test]
        public void When_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventCollected());
        }

        [Test]
        public void When_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(2, "message1", "info", "spanid", "traceid", _contextData),
                new LogEventWireModel(3, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventsSent(3));
        }

        [Test]
        public void When_more_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 105;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                var logEventsInner = new List<LogEventWireModel>
                {
                    new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
                };
                _logEventAggregator.CollectWithPriority(logEventsInner, 1.0F);
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;
            if (logEvents == null) throw new ArgumentNullException(nameof(logEvents));
            var actualAddAttempts = logEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void When_less_than_reservoir_size_events_are_reported_number_events_seen_is_accurate()
        {
            var expectedAddAttempts = 99;

            for (var i = 0; i < expectedAddAttempts; i++)
            {
                var logEventsInner = new List<LogEventWireModel>
                {
                    new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
                };
                _logEventAggregator.CollectWithPriority(logEventsInner, 1.0F);
            }

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;
            if (logEvents == null) throw new ArgumentNullException(nameof(logEvents));
            var actualAddAttempts = logEvents.GetAddAttemptsCount();

            _harvestAction();

            Assert.AreEqual(expectedAddAttempts, actualAddAttempts);
        }

        [Test]
        public void When_harvest_occurs_default_reservoir_size_is_reported_accurately()
        {
            const uint expectedReservoirSize = 100;

            var logEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData)
            };
            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var actualReservoirSize = privateAccessor.CallMethod("GetReservoirSize");

            _harvestAction();

            Assert.AreEqual(expectedReservoirSize, actualReservoirSize);
        }

        [Test]
        public void When_error_events_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _logEventAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.LogEventCollectorEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _logEventAggregator = new LogEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }

        [Test]
        public void Harvest_cycle_should_match_configured_cycle()
        {
            Assert.AreEqual(ConfiguredHarvestCycle, _harvestCycle);
        }

        [Test]
        public void Logs_Dropped_Metric_is_reported_when_capacity_is_exceeded()
        {
            // Arrange
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<LogEventWireModelCollection>()))
                .ReturnsAsync(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);

            var logEvents = new List<LogEventWireModel>();
            for (var i = 0; i < 105; i++)
            {
                logEvents.Add(new LogEventWireModel(1, "message1", "info", "spanid", "traceid", _contextData));
            }

            _logEventAggregator.CollectWithPriority(logEvents, 1.0F);

            // Act
            _harvestAction();

            // Assert
            // The number of logs over capacity (100) should be reported as dropped
            Mock.Assert(() => _agentHealthReporter.ReportLoggingEventsDropped(5));
        }

        #region Helpers

        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.LogEventCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogEventsMaxSamplesStored).Returns(100);
            Mock.Arrange(() => configuration.ApplicationNames).Returns(new List<string> { "appname1" });
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

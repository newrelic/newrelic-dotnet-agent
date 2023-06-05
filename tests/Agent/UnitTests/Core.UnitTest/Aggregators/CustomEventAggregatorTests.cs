// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using NewRelic.Core;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class CustomEventAggregatorTests
    {

        private readonly Dictionary<string, object> _emptyCustomAttributes = new Dictionary<string, object>();

        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private CustomEventAggregator _customEventAggregator;
        private IProcessStatic _processStatic;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private static readonly TimeSpan ConfiguredHarvestCycle = TimeSpan.FromSeconds(5);
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.CustomEventsHarvestCycle).Returns(ConfiguredHarvestCycle);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _processStatic = Mock.Create<IProcessStatic>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _customEventAggregator = new CustomEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        private IAttributeValueCollection GetCustomEventAttribs()
        {
            var result = new AttributeValueCollection(AttributeDestinations.CustomEvent);
            _attribDefs.CustomEventType.TrySetValue(result, "event_type");
            _attribDefs.Timestamp.TrySetDefault(result);

            return result;
        }

        [TearDown]
        public void TearDown()
        {
            _customEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        #region Configuration

        [Test]
        public void Collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentEvents = null as IEnumerable<CustomEventWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .DoInstead<IEnumerable<CustomEventWireModel>>(events => sentEvents = events);
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Act
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void Collections_are_resized_on_configuration_update_event()
        {
            // Arrange
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.CustomEventsMaximumSamplesStored).Returns(2);  //set the reservoir to only two
            Mock.Arrange(() => configuration.ConfigurationVersion).Returns(configuration.ConfigurationVersion + 1);
            var sentEvents = Enumerable.Empty<CustomEventWireModel>();
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .DoInstead<IEnumerable<CustomEventWireModel>>(events => sentEvents = events);

            //ordered by priority descending
            var eventsToSend = new[]
            {
                new CustomEventWireModel(0.3f, GetCustomEventAttribs()),
                new CustomEventWireModel(0.2f, GetCustomEventAttribs()),
                new CustomEventWireModel(0.1f, GetCustomEventAttribs())
            };

            // Act
            //collect 3 events, all should be retained - reservoir should be default size of 10000
            eventsToSend.ForEach(_customEventAggregator.Collect);

            _harvestAction();

            // Assert that all three of the events were retained and sent.
            Assert.That(sentEvents, Has.Exactly(3).Items);
            Assert.That(sentEvents, Is.EqualTo(eventsToSend));

            //this event will resize the reservoir (deleting it's contents).  The Collect() method must be called after this.
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));

            //collect 3 events, only two should be in the reservoir
            eventsToSend.ForEach(_customEventAggregator.Collect);

            _harvestAction();

            // Assert that only two of the events were retained and sent.
            Assert.That(sentEvents, Has.Exactly(2).Items);
            Assert.That(sentEvents, Is.EqualTo(eventsToSend.OrderByDescending(x => x.Priority).Take(2)));
        }

        #endregion

        [Test]
        public void Events_send_on_harvest()
        {
            // Arrange
            var sentEvents = null as IEnumerable<CustomEventWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .DoInstead<IEnumerable<CustomEventWireModel>>(events => sentEvents = events);

            //ordered by priority descending
            var eventsToSend = new[]
            {
                new CustomEventWireModel(0.3f, GetCustomEventAttribs()),
                new CustomEventWireModel(0.2f, GetCustomEventAttribs()),
                new CustomEventWireModel(0.1f, GetCustomEventAttribs())
            };

            eventsToSend.ForEach(_customEventAggregator.Collect);

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
            _customEventAggregator.Collect(new CustomEventWireModel(0.3f, GetCustomEventAttribs()));

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
        }

        [Test]
        public void Events_sent_reported_on_harvest()
        {
            // Arrange
            _customEventAggregator.Collect(new CustomEventWireModel(0.3f, GetCustomEventAttribs()));

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(1));
        }

        [Test]
        public void Reservoir_resized_reported_on_post_too_big_response()
        {
            // Arrange
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.2f, GetCustomEventAttribs()));
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .ReturnsAsync(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventReservoirResized(1));
        }

        [Test]
        public void Events_are_not_sent_if_there_are_no_events_to_send()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
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
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });

            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.2f, GetCustomEventAttribs()));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
        }

        [Test]
        public void Events_are_not_retained_after_harvest_if_response_equals_discard()
        {
            // Arrange
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.Discard);
                });
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.2f, GetCustomEventAttribs()));
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
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEventCount = events.Count();
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.2f, GetCustomEventAttribs()));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(2, sentEventCount);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(2));
        }

        [Test]
        public void Half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEventCount = events.Count();
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.11f, GetCustomEventAttribs()));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentEventCount);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(2));
        }

        [Test]
        public void Zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
        {
            // Arrange
            IEnumerable<CustomEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns<IEnumerable<CustomEventWireModel>>(events =>
                {
                    sentEvents = events;
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentEvents);
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(1));
        }

        [Test]
        public void When_no_events_are_published_then_no_events_are_reported_to_agent_health()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsRecollected(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventReservoirResized(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(Arg.IsAny<int>()), Occurs.Never());
        }

        [Test]
        public void When_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            _customEventAggregator.Collect(Mock.Create<CustomEventWireModel>());

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventCollected());
        }

        [Test]
        public void When_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            _customEventAggregator.Collect(new CustomEventWireModel(0.1f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.2f, GetCustomEventAttribs()));
            _customEventAggregator.Collect(new CustomEventWireModel(0.3f, GetCustomEventAttribs()));

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportCustomEventsSent(3));
        }

        [Test]
        public void When_custom_events_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _customEventAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _customEventAggregator = new CustomEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

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
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.CustomEventsMaximumSamplesStored).Returns(10000);
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

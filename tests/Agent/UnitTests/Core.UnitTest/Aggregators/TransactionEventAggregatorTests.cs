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
using NewRelic.Core;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class TransactionEventAggregatorTests
    {
        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private TransactionEventAggregator _transactionEventAggregator;
        private IProcessStatic _processStatic;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private static readonly TimeSpan ConfiguredHarvestCycle = TimeSpan.FromSeconds(5);

        private const string TimeStampKey = "timestamp";
        private readonly static AttributeValueCollection _attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent);

        private const int MaxSamplesStored = 10000;

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.TransactionEventsHarvestCycle).Returns(ConfiguredHarvestCycle);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _processStatic = Mock.Create<IProcessStatic>();

            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _transactionEventAggregator = new TransactionEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
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
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .DoInstead<IEnumerable<TransactionEventWireModel>>(events => sentEvents = events);
            var transactionEventWireModel = new TransactionEventWireModel(_attribValues, false, 0.3f);
            _transactionEventAggregator.Collect(transactionEventWireModel);

            // Act
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            _harvestAction();

            // Assert
            Assert.That(sentEvents, Is.Null);
        }

        #endregion

        [Test]
        public void Events_send_on_harvest()
        {
            // Arrange
            var sentEvents = null as IEnumerable<TransactionEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .DoInstead<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) => sentEvents = events);

            var eventsToSend = new[]
            {
                new TransactionEventWireModel(_attribValues, false, 0.3f),
                new TransactionEventWireModel(_attribValues, false, 0.2f),
                new TransactionEventWireModel(_attribValues, false, 0.1f)
            };
            eventsToSend.ForEach(_transactionEventAggregator.Collect);

            // Act
            _harvestAction();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(sentEvents.Count(), Is.EqualTo(3));
                Assert.That(eventsToSend, Is.EqualTo(sentEvents));
            });
        }

        [Test]
        public void Valid_EventHarvestData()
        {
            // Arrange
            var actualReservoirSize = 0;
            var actualEventsSeen = 0;
            const uint expectedReservoirSize = MaxSamplesStored;

            var eventsToSend = new[]
            {
                new TransactionEventWireModel(_attribValues, false, 0.3f),
                new TransactionEventWireModel(_attribValues, false, 0.2f),
                new TransactionEventWireModel(_attribValues, false, 0.1f)
            };

            var expectedEventsSeen = eventsToSend.Length;

            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .DoInstead<EventHarvestData, IEnumerable<TransactionEventWireModel>>((eventHarvestData, events) =>
                {
                    actualReservoirSize = eventHarvestData.ReservoirSize;
                    actualEventsSeen = eventHarvestData.EventsSeen;
                });

            // Act
            eventsToSend.ForEach(_transactionEventAggregator.Collect);
            _harvestAction();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(actualReservoirSize, Is.EqualTo(expectedReservoirSize));
                Assert.That(actualEventsSeen, Is.EqualTo(expectedEventsSeen));
            });
        }

        [Test]
        public void Event_seen_reported_on_collect()
        {
            // Act
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected());
        }

        [Test]
        public void Events_sent_reported_on_harvest()
        {
            // Arrange
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));

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
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;
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
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<IEnumerable<TransactionEventWireModel>>(events =>
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
        public void Events_are_not_retained_after_harvest_if_response_equals_request_successful()
        {
            // Arrange
            IEnumerable<TransactionEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.RequestSuccessful;
                });
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.That(sentEvents, Is.Null);
        }

        [Test]
        public void Events_are_not_retained_after_harvest_if_response_equals_discard()
        {
            // Arrange
            IEnumerable<TransactionEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.Discard;
                });
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.That(sentEvents, Is.Null);
        }

        [Test]
        public void Events_are_retained_after_harvest_if_response_equals_retain()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.Retain;
                });
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.That(sentEventCount, Is.EqualTo(2));
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
        }

        [Test]
        public void Half_of_the_events_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;
                });
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            _harvestAction();
            sentEventCount = int.MinValue; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.That(sentEventCount, Is.EqualTo(1));
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(2));
        }

        [Test]
        public void Zero_events_are_retained_after_harvest_if_response_equals_post_too_big_error_with_only_one_event_in_post()
        {
            // Arrange
            IEnumerable<TransactionEventWireModel> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<TransactionEventWireModel>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;
                });
            var transactionEventWireModel = new TransactionEventWireModel(_attribValues, false, 0.3f);
            _transactionEventAggregator.Collect(transactionEventWireModel);
            _harvestAction();
            sentEvents = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.That(sentEvents, Is.Null);
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(1));
        }

        [Test]
        public void When_no_events_are_published_then_no_events_are_reported_to_agent_health()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsRecollected(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventReservoirResized(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsSent(Arg.IsAny<int>()), Occurs.Never());
        }

        [Test]
        public void When_event_is_collected_then_events_seen_is_reported_to_agent_health()
        {
            // Act
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventCollected());
        }

        [Test]
        public void When_harvesting_events_then_event_sent_is_reported_to_agent_health()
        {
            // Arrange
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.2f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.1f));
            _transactionEventAggregator.Collect(new TransactionEventWireModel(_attribValues, false, 0.3f));

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportTransactionEventsSent(3));
        }

        [Test]
        public void When_transaction_events_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _transactionEventAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _transactionEventAggregator = new TransactionEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }

        [Test]
        public void Harvest_cycle_should_match_configured_cycle()
        {
            Assert.That(_harvestCycle, Is.EqualTo(ConfiguredHarvestCycle));
        }

        #region Helpers

        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.TransactionEventsMaximumSamplesStored).Returns(MaxSamplesStored);
            Mock.Arrange(() => configuration.TransactionEventsTransactionsEnabled).Returns(true);
            Mock.Arrange(() => configuration.TransactionEventsAttributesEnabled).Returns(true);
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

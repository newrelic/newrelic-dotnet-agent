// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Segments;
using NewRelic.Core;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.Core.Spans.Tests
{
    internal class SpanEventAggregatorTests
    {
        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private SpanEventAggregator _spanEventAggregator;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IProcessStatic _processStatic;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private static readonly TimeSpan ConfiguredHarvestCycle = TimeSpan.FromSeconds(5);

        private bool _actualWillHarvest;

        private const string TimeStampKey = "timestamp";
        private const string PriorityKey = "priority";
        private static readonly DateTime TestTime = DateTime.UtcNow;

        private static readonly long TestTimeNbr = TestTime.ToUnixTimeMilliseconds();

        private static ISpanEventWireModel[] CreateSpanEventModels()
        {
            var result = new List<ISpanEventWireModel>();

            var attribSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            var attribDefs = attribSvc.AttributeDefs;

            {
                var spanAttribs = new SpanAttributeValueCollection();
                spanAttribs.Priority = 0.654321f;
                attribDefs.Timestamp.TrySetValue(spanAttribs, TestTime);
                attribDefs.Priority.TrySetValue(spanAttribs, spanAttribs.Priority);
                result.Add(spanAttribs);
            }

            {
                var spanAttribs = new SpanAttributeValueCollection();
                spanAttribs.Priority = 0.654321f;
                attribDefs.Timestamp.TrySetValue(spanAttribs, TestTime.AddMilliseconds(1));
                attribDefs.Priority.TrySetValue(spanAttribs, spanAttribs.Priority);
                result.Add(spanAttribs);
            }

            {
                var spanAttribs = new SpanAttributeValueCollection();
                spanAttribs.Priority = 0.1f;
                attribDefs.Timestamp.TrySetValue(spanAttribs, TestTime);
                attribDefs.Priority.TrySetValue(spanAttribs, spanAttribs.Priority);
                result.Add(spanAttribs);
            }


            return result.ToArray();
        }

        private static readonly ISpanEventWireModel[] SpanEvents = CreateSpanEventModels();

        #region Helpers
        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.SpanEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.DistributedTracingEnabled).Returns(true);
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
            Mock.Arrange(() => configuration.SpanEventsHarvestCycle).Returns(ConfiguredHarvestCycle);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            Mock.Create<IDataStreamingService<Span, SpanBatch, RecordStatus>>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _processStatic = Mock.Create<IProcessStatic>();

            _scheduler = Mock.Create<IScheduler>();

            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) =>
                {
                    _harvestAction = action;
                    _harvestCycle = harvestCycle;
                    _actualWillHarvest = true;
                });

            Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan?>((action, __) =>
                {
                    _actualWillHarvest = false;
                });

            _actualWillHarvest = false;

            _spanEventAggregator = new SpanEventAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _spanEventAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void EventsSendOnHarvest()
        {
            // Arrange
            const int EventCount = 3;
            const int ExpectedReservoirSize = 1000;
            const int ExpectedEventsSeen = EventCount;
            var actualReservoirSize = int.MaxValue;
            var actualEventsSeen = int.MaxValue;
            var sentEvents = null as IEnumerable<ISpanEventWireModel>;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ISpanEventWireModel>>(), Arg.IsAny<string>()))
                .DoInstead<EventHarvestData, IEnumerable<ISpanEventWireModel>>((eventHarvestData, events) =>
                {
                    sentEvents = events;
                    actualReservoirSize = eventHarvestData.ReservoirSize;
                    actualEventsSeen = eventHarvestData.EventsSeen;
                });

            CollectSpanEvents(EventCount);

            // Act
            _harvestAction();

            // Assert
            var SpanAttributeValueCollections = sentEvents as SpanAttributeValueCollection[] ?? sentEvents.ToArray();
            Assert.That(SpanAttributeValueCollections, Has.Exactly(EventCount).Items);
            Assert.Multiple(() =>
            {
                Assert.That(SpanAttributeValueCollections, Is.EqualTo(SpanEvents));
                Assert.That(actualReservoirSize, Is.EqualTo(ExpectedReservoirSize));
                Assert.That(actualEventsSeen, Is.EqualTo(ExpectedEventsSeen));
            });
        }

        [Test]
        public void EventSeenReportedOnCollect()
        {
            const int eventCount = 1;
            // Act
            CollectSpanEvents(eventCount);

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
        }

        [Test]
        public void EventsSentReportedOnHarvest()
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
        public void EventsAreNotSentIfThereAreNoEventsToSend()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanAttributeValueCollection>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<SpanAttributeValueCollection>>((_, events) =>
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
        public void EventsAreNotRetainedAfterHarvestIfResponseEqualsDiscard()
        {
            // Arrange
            IEnumerable<SpanAttributeValueCollection> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanAttributeValueCollection>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<SpanAttributeValueCollection>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.Discard;
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
        public void EventsAreRetainedAfterHarvestIfResponseEqualsRetain()
        {
            const int eventCount = 2;
            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ISpanEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<ISpanEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.Retain;
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
        public void MetricsAreCorrectAfterHarvestRetryIfSendResponseEqualsDiscard()
        {
            const int eventCount = 2;
            // Arrange
            var firstTime = true;
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ISpanEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<ISpanEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    var returnValue = firstTime ? DataTransportResponseStatus.Retain : DataTransportResponseStatus.RequestSuccessful;
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
        public void HalfOfTheEventsAreRetainedAfterHarvestIfResponseEqualsPostTooBigError()
        {
            const int eventCount = 2;

            // Arrange
            var sentEventCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ISpanEventWireModel>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<ISpanEventWireModel>>((_, events) =>
                {
                    sentEventCount = events.Count();
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;
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
        public void ZeroEventsAreRetainedAfterHarvestIfResponseEqualsPostTooBigErrorWithOnlyOneEventInPost()
        {
            const int eventCount = 1;
            // Arrange
            IEnumerable<SpanAttributeValueCollection> sentEvents = null;
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanAttributeValueCollection>>(), Arg.IsAny<string>()))
                .Returns<EventHarvestData, IEnumerable<SpanAttributeValueCollection>>((_, events) =>
                {
                    sentEvents = events;
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;
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
        public void WhenNoEventsArePublishedThenNoEventsAreReportedToAgentHealth()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(Arg.IsAny<int>()), Occurs.Never());
        }

        [Test]
        public void WhenEventIsCollectedThenEventsSeenIsReportedToAgentHealth()
        {
            const int eventCount = 1;
            // Act
            CollectSpanEvents(eventCount);

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
        }

        [Test]
        public void WhenHarvestingEventsThenEventSentIsReportedToAgentHealth()
        {
            const int eventCount = 3;
            // Arrange
            CollectSpanEvents(eventCount);

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(eventCount));
        }

        [Test]
        public void TraditionalTracingEnabled_HarvestScheduled
        (
            [Values(true, false)] bool spanEventsEnabled,
            [Values(true, false)] bool distributedTracingEnabled
        )
        {
            Mock.Arrange(() => _configurationAutoResponder.Configuration.SpanEventsEnabled).Returns(spanEventsEnabled);
            Mock.Arrange(() => _configurationAutoResponder.Configuration.DistributedTracingEnabled).Returns(distributedTracingEnabled);
            Mock.Arrange(() => _configurationAutoResponder.Configuration.SpanEventsMaxSamplesStored).Returns(10000);

            var expectedShouldHarvest = spanEventsEnabled && distributedTracingEnabled;

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Assert.That(_actualWillHarvest, Is.EqualTo(expectedShouldHarvest), "Is Harvesting");
        }

        [Test]
        public void Harvest_cycle_should_match_configured_cycle()
        {
            Assert.That(_harvestCycle, Is.EqualTo(ConfiguredHarvestCycle));
        }

        [Test]
        public void IsEnabledBasedOnConfigSettings
        (
            [Values(true, false)] bool distributedTracingEnabled,
            [Values(true, false)] bool spanEventsEnabled,
            [Values(10000, 0, -1)] int reserviorSize
        )
        {
            var expectedIsEnabled = distributedTracingEnabled && spanEventsEnabled && reserviorSize > 0;

            Mock.Arrange(() => _configurationAutoResponder.Configuration.SpanEventsEnabled).Returns(spanEventsEnabled);
            Mock.Arrange(() => _configurationAutoResponder.Configuration.DistributedTracingEnabled).Returns(distributedTracingEnabled);
            Mock.Arrange(() => _configurationAutoResponder.Configuration.SpanEventsMaxSamplesStored).Returns(reserviorSize);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            NrAssert.Multiple
            (
                () => Assert.That(_spanEventAggregator.IsServiceEnabled, Is.EqualTo(expectedIsEnabled), "Service Enabled"),
                () => Assert.That(_spanEventAggregator.IsServiceAvailable, Is.EqualTo(expectedIsEnabled), "Service Enabled")
            );
        }

    }
}

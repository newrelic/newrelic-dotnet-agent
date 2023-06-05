// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class ErrorTraceAggregatorTests
    {
        private ErrorTraceAggregator _errorTraceAggregator;
        private IDataTransportService _dataTransportService;
        private IAgentHealthReporter _agentHealthReporter;
        private IProcessStatic _processStatic;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;

        [SetUp]
        public void SetUp()
        {
            var configuration = GetDefaultConfiguration();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _processStatic = Mock.Create<IProcessStatic>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            _errorTraceAggregator = new ErrorTraceAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _errorTraceAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void When_error_traces_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _errorTraceAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _errorTraceAggregator = new ErrorTraceAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }

        #region Conifiguration

        [Test]
        public void collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentErrors = null as IEnumerable<ErrorTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .DoInstead<IEnumerable<ErrorTraceWireModel>>(errors => sentErrors = errors);
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());

            // Act
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            _harvestAction();

            // Assert
            Assert.Null(sentErrors);
        }

        #endregion

        #region Harvest

        [Test]
        public void error_traces_send_on_harvest()
        {
            // Arrange
            var sentErrors = null as IEnumerable<ErrorTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .DoInstead<IEnumerable<ErrorTraceWireModel>>(errors => sentErrors = errors);
            var errorsToSend = new[]
            {
                Mock.Create<ErrorTraceWireModel>(),
                Mock.Create<ErrorTraceWireModel>(),
                Mock.Create<ErrorTraceWireModel>()
            };
            errorsToSend.ForEach(_errorTraceAggregator.Collect);

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(3, sentErrors.Count());
            Assert.AreEqual(sentErrors, errorsToSend);
        }

        [Test]
        public void nothing_is_sent_on_harvest_if_there_are_no_error_traces()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    sendCalled = true;  
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });

            // Act
            _harvestAction();

            // Assert
            Assert.False(sendCalled);
        }

        #endregion

        #region Retention

        [Test]
        public void zero_error_traces_are_retained_after_harvest_if_response_equals_request_successful()
        {
            // Arrange
            IEnumerable<ErrorTraceWireModel> sentErrors = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    sentErrors = errors;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _harvestAction();
            sentErrors = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentErrors);
        }

        [Test]
        public void zero_error_traces_are_retained_after_harvest_if_response_equals_discard()
        {
            // Arrange
            IEnumerable<ErrorTraceWireModel> sentErrors = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    sentErrors = errors;
                    return Task.FromResult(DataTransportResponseStatus.Discard);
                });
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _harvestAction();
            sentErrors = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentErrors);
        }

        [Test]
        public void error_traces_are_retained_after_harvest_if_response_equals_retain()
        {
            // Arrange
            var sentErrorsCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    sentErrorsCount = errors.Count();
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());

            // Act
            _harvestAction();
            sentErrorsCount = int.MinValue; // reset
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentErrorsCount);
        }

        [Test]
        public void zero_error_traces_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            IEnumerable<ErrorTraceWireModel> sentErrors = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    sentErrors = errors;
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());

            // Act
            _harvestAction();
            sentErrors = null; // reset
            _harvestAction();

            // Assert
            Assert.Null(sentErrors);
        }

        #endregion

        #region Agent Health Reporting

        [Test]
        public void error_trace_collected_is_reported_to_agent_health()
        {
            // Arrange
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorTraceCollected());
        }

        [Test]
        public void error_trace_sent_is_reported_to_agent_health()
        {
            // Arrange
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorTracesSent(1));
        }

        [Test]
        public void error_trace_recollected_is_reported_to_agent_health()
        {
            // Arrange
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns<IEnumerable<ErrorTraceWireModel>>(errors =>
                {
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });
            _errorTraceAggregator.Collect(Mock.Create<ErrorTraceWireModel>());
            _harvestAction();

            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorTracesRecollected(1));
        }

        [Test]
        public void nothing_is_reported_to_agent_health_when_there_are_no_error_traces()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportErrorTraceCollected(), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportErrorTracesRecollected(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportErrorTracesSent(Arg.IsAny<int>()), Occurs.Never());
        }

        #endregion

        #region Helpers

        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.ErrorsMaximumPerPeriod).Returns(20);
            Mock.Arrange(() => configuration.CaptureErrorCollectorAttributes).Returns(true);
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        #endregion
    }
}

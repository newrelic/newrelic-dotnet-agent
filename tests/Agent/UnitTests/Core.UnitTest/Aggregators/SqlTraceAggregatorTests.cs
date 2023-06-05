// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class SqlTraceAggregatorTests
    {
        private SqlTraceAggregator _sqlTraceAggregator;
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
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _processStatic = Mock.Create<IProcessStatic>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            _sqlTraceAggregator = new SqlTraceAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _sqlTraceAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void When_sql_traces_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _sqlTraceAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.SlowSqlEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _sqlTraceAggregator = new SqlTraceAggregator(_dataTransportService, _scheduler, _processStatic, _agentHealthReporter);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }

        #region Aggregation

        [Test]
        public void traces_are_aggregated_if_same_sql_id()
        {
            // Arrange
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(
                    1,
                    transactionName: "transactionName1",
                    sql: "sql1",
                    uri: "uri1",
                    datastoreMetricName: "datastoreMetricName1",
                    callCount: 1,
                    minCallTime: TimeSpan.FromSeconds(5),
                    maxCallTime: TimeSpan.FromSeconds(5),
                    totalCallTime: TimeSpan.FromSeconds(5),
                    parameterData: new Dictionary<string, object> { { "foo", "bar" } }
                    ));
            sqlTracesToSend.Insert(GetSqlTrace(
                    1,
                    transactionName: "transactionName2",
                    sql: "sql2",
                    uri: "uri2",
                    datastoreMetricName: "datastoreMetricName2",
                    callCount: 1,
                    minCallTime: TimeSpan.FromSeconds(3),
                    maxCallTime: TimeSpan.FromSeconds(3),
                    totalCallTime: TimeSpan.FromSeconds(3),
                    parameterData: new Dictionary<string, object> { { "zip", "zap" } }
                    ));

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentSqlTraces.Count());
            var trace = sentSqlTraces.First();

            NrAssert.Multiple(
                () => Assert.AreEqual(1, sentSqlTraces.Count()),
                () => Assert.AreEqual(1, trace.SqlId),
                () => Assert.AreEqual("transactionName1", trace.TransactionName),
                () => Assert.AreEqual("sql1", trace.Sql),
                () => Assert.AreEqual("uri1", trace.Uri),
                () => Assert.AreEqual("datastoreMetricName1", trace.DatastoreMetricName),
                () => Assert.AreEqual(2, trace.CallCount),
                () => Assert.AreEqual(TimeSpan.FromSeconds(3), trace.MinCallTime),
                () => Assert.AreEqual(TimeSpan.FromSeconds(5), trace.MaxCallTime),
                () => Assert.AreEqual(TimeSpan.FromSeconds(8), trace.TotalCallTime),

                () => Assert.AreEqual(1, trace.ParameterData.Count),
                () => Assert.AreEqual("bar", trace.ParameterData["foo"])
                );
        }

        #endregion Aggregation

        #region Configuration

        [Test]
        public void collections_are_reset_on_configuration_update_event()
        {
            // Arrange
            var configuration = GetDefaultConfiguration(int.MaxValue);
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);
            _sqlTraceAggregator.Collect(new SqlTraceStatsCollection());

            // Act
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            _harvestAction();

            // Assert
            Assert.Null(sentSqlTraces);
        }

        [Test]
        public void slowest_traces_are_retained_if_too_many_traces()
        {
            // Arrange
            var sqlTracesPerPeriod = 5;
            var configuration = GetDefaultConfiguration(int.MaxValue, sqlTracesPerPeriod);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            sqlTracesToSend.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(999)));
            sqlTracesToSend.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(30)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(40)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(60)));
            sqlTracesToSend.Insert(GetSqlTrace(7, maxCallTime: TimeSpan.FromSeconds(70)));

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            NrAssert.Multiple(
                () => Assert.AreEqual(sqlTracesPerPeriod, sentSqlTraces.Count()),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 2)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 4)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 5)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 6)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 7))
                );
        }

        #endregion Configuration

        #region SqlStatsCollection

        [Test]
        public void concurrent_dictionary_limits_maxTraces()
        {
            var maxTraces = 10;
            var sqlTrStats = new SqlTraceStatsCollection(maxTraces);

            sqlTrStats.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            sqlTrStats.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(20)));
            sqlTrStats.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(30)));
            sqlTrStats.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(40)));
            sqlTrStats.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTrStats.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(60)));
            sqlTrStats.Insert(GetSqlTrace(7, maxCallTime: TimeSpan.FromSeconds(70)));
            sqlTrStats.Insert(GetSqlTrace(8, maxCallTime: TimeSpan.FromSeconds(80)));
            sqlTrStats.Insert(GetSqlTrace(9, maxCallTime: TimeSpan.FromSeconds(90)));
            sqlTrStats.Insert(GetSqlTrace(10, maxCallTime: TimeSpan.FromSeconds(100)));
            sqlTrStats.Insert(GetSqlTrace(11, maxCallTime: TimeSpan.FromSeconds(110)));

            Assert.AreEqual(maxTraces, sqlTrStats.Collection.Count);
        }

        [Test]
        public void slowest_traces_retained_when_random_order_and_aggregation()
        {
            // Arrange
            var sqlTracesPerPeriod = 5;
            var configuration = GetDefaultConfiguration(int.MaxValue, sqlTracesPerPeriod);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection(maxTraces: 10);
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(40)));  // aggregate
            sqlTracesToSend.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(30)));
            sqlTracesToSend.Insert(GetSqlTrace(8, maxCallTime: TimeSpan.FromSeconds(35)));
            sqlTracesToSend.Insert(GetSqlTrace(9, maxCallTime: TimeSpan.FromSeconds(45)));
            sqlTracesToSend.Insert(GetSqlTrace(10, maxCallTime: TimeSpan.FromSeconds(33)));
            sqlTracesToSend.Insert(GetSqlTrace(11, maxCallTime: TimeSpan.FromSeconds(34)));
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(60)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(51)));  // aggregate
            sqlTracesToSend.Insert(GetSqlTrace(7, maxCallTime: TimeSpan.FromSeconds(70)));
            sqlTracesToSend.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(999))); // beyond max limit

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            NrAssert.Multiple(
                () => Assert.AreEqual(sqlTracesPerPeriod, sentSqlTraces.Count()),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 2)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 4)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 5)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 6)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 7))
                );
        }

        [Test]
        public void all_traces_same_duration_send_last_detected()
        {
            // Arrange
            var sqlTracesPerPeriod = 5;
            var configuration = GetDefaultConfiguration(int.MaxValue, sqlTracesPerPeriod);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection(maxTraces: 5);
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(50)));
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(50)));

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            NrAssert.Multiple(
                () => Assert.AreEqual(sqlTracesPerPeriod, sentSqlTraces.Count()),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 5)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 4)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 3)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 2)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 1))
                );
        }

        [Test]
        public void send_slowest_traces_multiple_collections_longest_aggregates_last()
        {
            // Arrange
            var sqlTracesPerPeriod = 5;
            var configuration = GetDefaultConfiguration(int.MaxValue, sqlTracesPerPeriod);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection(maxTraces: 5);
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(30)));
            sqlTracesToSend.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(31)));
            sqlTracesToSend.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(32)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(33)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(34)));  // shorter aggregate
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(35)));  // shorter aggregate

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            sqlTracesToSend.Insert(GetSqlTrace(10, maxCallTime: TimeSpan.FromSeconds(49)));
            sqlTracesToSend.Insert(GetSqlTrace(11, maxCallTime: TimeSpan.FromSeconds(25)));
            sqlTracesToSend.Insert(GetSqlTrace(12, maxCallTime: TimeSpan.FromSeconds(48)));
            sqlTracesToSend.Insert(GetSqlTrace(13, maxCallTime: TimeSpan.FromSeconds(32)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(47)));  // longest aggregate
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(46)));  // longest aggregate

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            NrAssert.Multiple(
                () => Assert.AreEqual(sqlTracesPerPeriod, sentSqlTraces.Count()),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 10)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 12)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 5)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 6)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 4))
                );
        }

        [Test]
        public void send_slowest_traces_multiple_collections_longest_aggregates_first()
        {
            // Arrange
            var sqlTracesPerPeriod = 5;
            var configuration = GetDefaultConfiguration(int.MaxValue, sqlTracesPerPeriod);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(configuration, ConfigurationUpdateSource.Local));
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection(maxTraces: 5);
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(30)));
            sqlTracesToSend.Insert(GetSqlTrace(2, maxCallTime: TimeSpan.FromSeconds(31)));
            sqlTracesToSend.Insert(GetSqlTrace(3, maxCallTime: TimeSpan.FromSeconds(32)));
            sqlTracesToSend.Insert(GetSqlTrace(4, maxCallTime: TimeSpan.FromSeconds(33)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(47)));  // longest aggregate
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(46)));  // longest aggregate

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            sqlTracesToSend.Insert(GetSqlTrace(10, maxCallTime: TimeSpan.FromSeconds(49)));
            sqlTracesToSend.Insert(GetSqlTrace(11, maxCallTime: TimeSpan.FromSeconds(25)));
            sqlTracesToSend.Insert(GetSqlTrace(12, maxCallTime: TimeSpan.FromSeconds(48)));
            sqlTracesToSend.Insert(GetSqlTrace(13, maxCallTime: TimeSpan.FromSeconds(32)));
            sqlTracesToSend.Insert(GetSqlTrace(5, maxCallTime: TimeSpan.FromSeconds(34)));  // shorter aggregate
            sqlTracesToSend.Insert(GetSqlTrace(6, maxCallTime: TimeSpan.FromSeconds(35)));  // shorter aggregate

            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            NrAssert.Multiple(
                () => Assert.AreEqual(sqlTracesPerPeriod, sentSqlTraces.Count()),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 10)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 12)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 5)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 6)),
                () => Assert.IsTrue(sentSqlTraces.Any(trace => trace.SqlId == 4))
                );
        }

        #endregion SqlStatsCollection

        #region Harvest

        [Test]
        public void sql_traces_send_on_harvest()
        {
            // Arrange
            var sentSqlTraces = null as IEnumerable<SqlTraceWireModel>;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .DoInstead<IEnumerable<SqlTraceWireModel>>(sqlTraces => sentSqlTraces = sqlTraces);

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1));
            sqlTracesToSend.Insert(GetSqlTrace(2));
            sqlTracesToSend.Insert(GetSqlTrace(3));
            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();

            // Assert
            Assert.AreEqual(3, sentSqlTraces.Count());
            Assert.AreEqual(sentSqlTraces, sqlTracesToSend.Collection.Values.ToList());
        }

        [Test]
        public void nothing_is_sent_on_harvest_if_there_are_no_sql_traces()
        {
            // Arrange
            var sendCalled = false;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns<IEnumerable<SqlTraceWireModel>>(sqlTraces =>
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
        public void zero_sql_traces_are_retained_after_harvest_if_response_equals_request_successful()
        {
            // Arrange
            IEnumerable<SqlTraceWireModel> sentSqlTraces = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns<IEnumerable<SqlTraceWireModel>>(sqlTraces =>
                {
                    sentSqlTraces = sqlTraces;
                    return Task.FromResult(DataTransportResponseStatus.RequestSuccessful);
                });

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            _sqlTraceAggregator.Collect(sqlTracesToSend);
            _sqlTraceAggregator.Collect(sqlTracesToSend);

            _harvestAction();
            sentSqlTraces = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentSqlTraces);
        }

        [Test]
        public void zero_sql_traces_are_retained_after_harvest_if_response_equals_discard()
        {
            // Arrange
            IEnumerable<SqlTraceWireModel> sentSqlTraces = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns<IEnumerable<SqlTraceWireModel>>(sqlTraces =>
                {
                    sentSqlTraces = sqlTraces;
                    return Task.FromResult(DataTransportResponseStatus.Discard);
                });

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            _sqlTraceAggregator.Collect(sqlTracesToSend);
            _sqlTraceAggregator.Collect(sqlTracesToSend);

            _harvestAction();
            sentSqlTraces = null; // reset

            // Act
            _harvestAction();

            // Assert
            Assert.Null(sentSqlTraces);
        }

        [Test]
        public void sql_traces_are_retained_after_harvest_if_response_equals_retain()
        {
            // Arrange
            var sentSqlTracesCount = int.MinValue;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns<IEnumerable<SqlTraceWireModel>>(sqlTraces =>
                {
                    sentSqlTracesCount = sqlTraces.Count();
                    return Task.FromResult(DataTransportResponseStatus.Retain);
                });

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();
            sentSqlTracesCount = int.MinValue; // reset
            _harvestAction();

            // Assert
            Assert.AreEqual(1, sentSqlTracesCount);
        }

        [Test]
        public void zero_sql_traces_are_retained_after_harvest_if_response_equals_post_too_big_error()
        {
            // Arrange
            IEnumerable<SqlTraceWireModel> sentSqlTraces = null;
            Mock.Arrange(() => _dataTransportService.SendAsync(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns<IEnumerable<SqlTraceWireModel>>(sqlTraces =>
                {
                    sentSqlTraces = sqlTraces;
                    return Task.FromResult(DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard);
                });

            var sqlTracesToSend = new SqlTraceStatsCollection();
            sqlTracesToSend.Insert(GetSqlTrace(1, maxCallTime: TimeSpan.FromSeconds(10)));
            _sqlTraceAggregator.Collect(sqlTracesToSend);

            // Act
            _harvestAction();
            sentSqlTraces = null; // reset
            _harvestAction();

            // Assert
            Assert.Null(sentSqlTraces);
        }

        #endregion

        #region Agent Health Reporting

        [Test]
        public void nothing_is_reported_to_agent_health_when_there_are_no_sql_traces()
        {
            // Act
            _harvestAction();

            // Assert
            Mock.Assert(() => _agentHealthReporter.ReportSqlTracesRecollected(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _agentHealthReporter.ReportSqlTracesSent(Arg.IsAny<int>()), Occurs.Never());
        }

        #endregion

        #region testing



        #endregion testing

        #region Helpers

        private static IConfiguration GetDefaultConfiguration(int? versionNumber = null, int? sqlTracesPerPeriod = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.SqlTracesPerPeriod).Returns(sqlTracesPerPeriod ?? 10);
            Mock.Arrange(() => configuration.SlowSqlEnabled).Returns(true);
            Mock.Arrange(() => configuration.DefaultHarvestCycle).Returns(TimeSpan.FromMinutes(1));
            if (versionNumber.HasValue)
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
            return configuration;
        }

        private static SqlTraceWireModel GetSqlTrace(int sqlId, string sql = null, string transactionName = null, string uri = null, string datastoreMetricName = null, uint? callCount = null, TimeSpan? minCallTime = null, TimeSpan? maxCallTime = null, TimeSpan? totalCallTime = null, IDictionary<string, object> parameterData = null)
        {
            var sqlTrace = Mock.Create<SqlTraceWireModel>();
            Mock.Arrange(() => sqlTrace.SqlId).Returns(sqlId);
            Mock.Arrange(() => sqlTrace.Sql).Returns(sql ?? "sql");
            Mock.Arrange(() => sqlTrace.TransactionName).Returns(transactionName ?? "transactionName");
            Mock.Arrange(() => sqlTrace.Uri).Returns(uri ?? "uri");
            Mock.Arrange(() => sqlTrace.DatastoreMetricName).Returns(datastoreMetricName ?? "datastoreMetricName");
            Mock.Arrange(() => sqlTrace.CallCount).Returns(callCount ?? 1);
            Mock.Arrange(() => sqlTrace.MinCallTime).Returns(minCallTime ?? TimeSpan.FromSeconds(1));
            Mock.Arrange(() => sqlTrace.MaxCallTime).Returns(maxCallTime ?? TimeSpan.FromSeconds(1));
            Mock.Arrange(() => sqlTrace.TotalCallTime).Returns(totalCallTime ?? TimeSpan.FromSeconds(1));
            Mock.Arrange(() => sqlTrace.ParameterData).Returns(parameterData ?? new Dictionary<string, object>());

            return sqlTrace;
        }

        #endregion
    }
}

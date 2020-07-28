using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    [TestFixture]
    public class TransactionTransformerSqlTraceTests
    {
        private TransactionTransformer _transactionTransformer;
        private ITransactionMetricNameMaker _transactionMetricNameMaker;
        private ISegmentTreeMaker _segmentTreeMaker;
        private IMetricBuilder _metricBuilder;
        private IMetricNameService _metricNameService;
        private IMetricAggregator _metricAggregator;
        private IConfigurationService _configurationService;
        private IConfiguration _configuration;
        private ITransactionTraceAggregator _transactionTraceAggregator;
        private ITransactionTraceMaker _transactionTraceMaker;
        private ITransactionEventAggregator _transactionEventAggregator;
        private ITransactionEventMaker _transactionEventMaker;
        private ITransactionAttributeMaker _transactionAttributeMaker;
        private IErrorTraceAggregator _errorTraceAggregator;
        private IErrorTraceMaker _errorTraceMaker;
        private IErrorEventAggregator _errorEventAggregator;
        private IErrorEventMaker _errorEventMaker;
        private ISqlTraceAggregator _sqlTraceAggregator;
        private ISqlTraceMaker _sqlTraceMaker;

        // TransactionTransformerSqlTraceTests is modelled after TransactionTransformerTests, but more real (non-mock) objects are required so that appropriate segment trees get generated.
        [SetUp]
        public void SetUp()
        {
            // Mocks
            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => txName.IsWeb)))
                .Returns(new TransactionMetricName("WebTransaction", "TransactionName"));
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => !txName.IsWeb))).Returns(new TransactionMetricName("OtherTransaction", "TransactionName"));

            _metricAggregator = Mock.Create<IMetricAggregator>();

            _configuration = GetDefaultConfiguration();
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            _transactionTraceAggregator = Mock.Create<ITransactionTraceAggregator>();
            _transactionTraceMaker = Mock.Create<ITransactionTraceMaker>();
            _transactionEventAggregator = Mock.Create<ITransactionEventAggregator>();
            _transactionEventMaker = Mock.Create<ITransactionEventMaker>();
            _transactionAttributeMaker = Mock.Create<ITransactionAttributeMaker>();
            _errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
            _errorTraceMaker = Mock.Create<IErrorTraceMaker>();
            _errorEventAggregator = Mock.Create<IErrorEventAggregator>();
            _errorEventMaker = Mock.Create<IErrorEventMaker>();

            // Non-Mocks
            _segmentTreeMaker = new SegmentTreeMaker();

            _metricBuilder = GetSimpleMetricBuilder();

            var dataTransportService = Mock.Create<DataTransport.IDataTransportService>();
            var scheduler = Mock.Create<Time.IScheduler>();
            var processStatic = Mock.Create<SystemInterfaces.IProcessStatic>();
            var agentHealthReporter = Mock.Create<AgentHealth.IAgentHealthReporter>();
            _sqlTraceAggregator = new SqlTraceAggregator(dataTransportService, scheduler, processStatic, agentHealthReporter);

            _transactionAttributeMaker = new TransactionAttributeMaker();

            var databaseService = Mock.Create<Database.IDatabaseService>();
            _sqlTraceMaker = new SqlTraceMaker(_configurationService);

            // create TransactionTransformer
            _transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, _segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService, _transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker, _transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker, _sqlTraceAggregator, _sqlTraceMaker);
        }
        public IMetricBuilder GetSimpleMetricBuilder()
        {
            _metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            return new MetricWireModel.MetricBuilder(_metricNameService);
        }

        [Test]
        public void SqlTraceIsSentToAggregator()
        {
            var commandText = "Select * from Table1";
            var duration = TimeSpan.FromMilliseconds(500);
            var datastoreSegment = TestTransactions.BuildSegment(null, DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");
            var segments = Enumerable.Empty<Segment>();
            segments = segments.Concat(new[] { datastoreSegment });

            var transaction = TestTransactions.CreateTestTransactionWithSegments(segments);
            Mock.Arrange(() => _configuration.SlowSqlEnabled).Returns(true);
            Mock.Arrange(() => _configuration.SqlExplainPlanThreshold).Returns(TimeSpan.FromMilliseconds(100));

            var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
            var args = new object[] { transaction };
            privateTransactionTransformer.CallMethod("Transform", args);

            var privateSqlTraceStatsInAggregator = new PrivateAccessor(_sqlTraceAggregator).GetField("_sqlTraceStats");
            var privateSqlTraceStatsCollection = (SqlTraceStatsCollection)privateSqlTraceStatsInAggregator;
            var tracesCount = ((IDictionary<long, SqlTraceWireModel>)privateSqlTraceStatsCollection.Collection).Count;
            Assert.AreEqual(tracesCount, 1);
        }

        [Test]
        public void SqlTracesCollectedMetricIsAccurate()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

            Mock.Arrange(() => _configuration.SlowSqlEnabled).Returns(true);
            Mock.Arrange(() => _configuration.SqlExplainPlanThreshold).Returns(TimeSpan.FromMilliseconds(100));
            var segments = new List<Segment>();

            int nextId = 0;
            var txSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => txSegmentState.ParentSegmentId()).Returns(() =>
                nextId == 0 ? (int?)null : nextId);
            Mock.Arrange(() => txSegmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(() => ++nextId);

            var commandText = "Select * from Table1";
            var duration = TimeSpan.FromMilliseconds(500);
            var datastoreSegment1 = TestTransactions.BuildSegment(txSegmentState, DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");
            segments.Add(datastoreSegment1);

            commandText = "Select * from Table2";
            duration = TimeSpan.FromMilliseconds(1000);
            var datastoreSegment2 = TestTransactions.BuildSegment(txSegmentState, DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");
            segments.Add(datastoreSegment2);

            commandText = "Select * from Table2";
            duration = TimeSpan.FromMilliseconds(900);
            var datastoreSegment3 = TestTransactions.BuildSegment(txSegmentState, DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");
            segments.Add(datastoreSegment3);

            var transaction = TestTransactions.CreateTestTransactionWithSegments(segments);

            var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
            var args = new object[] { transaction };
            privateTransactionTransformer.CallMethod("Transform", args);

            string sqlTracesCollectedMetricName = "Supportability/SqlTraces/TotalSqlTracesCollected";
            Assert.IsTrue(generatedMetrics.TryGetValue(sqlTracesCollectedMetricName, out MetricDataWireModel data));
            Assert.AreEqual(3, data.Value0);
        }

        #region Helpers
        private static IConfiguration GetDefaultConfiguration()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.TransactionEventsMaxSamplesStored).Returns(10000);
            Mock.Arrange(() => configuration.TransactionEventsTransactionsEnabled).Returns(true);
            Mock.Arrange(() => configuration.CaptureTransactionEventsAttributes).Returns(true);
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.ErrorCollectorCaptureEvents).Returns(true);
            Mock.Arrange(() => configuration.CaptureErrorCollectorAttributes).Returns(true);
            Mock.Arrange(() => configuration.TransactionTracerEnabled).Returns(true);
            return configuration;
        }

        #endregion Helpers
    }
}

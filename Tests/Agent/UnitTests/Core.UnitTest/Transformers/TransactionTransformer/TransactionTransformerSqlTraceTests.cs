using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class TransactionTransformerSqlTraceTests
	{
		[NotNull]
		private TransactionTransformer _transactionTransformer;

		[NotNull]
		private ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private ISegmentTreeMaker _segmentTreeMaker;

		[NotNull]
		private IMetricNameService _metricNameService;

		[NotNull]
		private IMetricAggregator _metricAggregator;

		[NotNull]
		private IConfigurationService _configurationService;

		[NotNull]
		private IConfiguration _configuration;

		[NotNull]
		private ITransactionTraceAggregator _transactionTraceAggregator;

		[NotNull]
		private ITransactionTraceMaker _transactionTraceMaker;

		[NotNull]
		private ITransactionEventAggregator _transactionEventAggregator;

		[NotNull]
		private ITransactionEventMaker _transactionEventMaker;

		[NotNull]
		private ITransactionAttributeMaker _transactionAttributeMaker;

		[NotNull]
		private IErrorTraceAggregator _errorTraceAggregator;

		[NotNull]
		private IErrorTraceMaker _errorTraceMaker;

		[NotNull]
		private IErrorEventAggregator _errorEventAggregator;

		[NotNull]
		private IErrorEventMaker _errorEventMaker;

		[NotNull]
		private ISqlTraceAggregator _sqlTraceAggregator;

		[NotNull]
		private ISqlTraceMaker _sqlTraceMaker;

		[NotNull]
		private ISpanEventAggregator _spanEventAggregator;

		[NotNull]
		private ISpanEventMaker _spanEventMaker;

		private IAgentTimerService _agentTimerService;

		private IAttributeService _attributeService;

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
			_spanEventAggregator = Mock.Create<ISpanEventAggregator>();
			_spanEventMaker = Mock.Create<ISpanEventMaker>();
			_agentTimerService = Mock.Create<IAgentTimerService>();

			_attributeService = Mock.Create<IAttributeService>();

			// Non-Mocks
			_segmentTreeMaker = new SegmentTreeMaker();

			_metricNameService = Mock.Create<IMetricNameService>();
			Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<String>())).Returns<String>(name => name);

			var dataTransportService = Mock.Create<DataTransport.IDataTransportService>();
			var scheduler = Mock.Create<Time.IScheduler>();
			var processStatic = Mock.Create<SystemInterfaces.IProcessStatic>();
			var agentHealthReporter = Mock.Create<AgentHealth.IAgentHealthReporter>();
			_sqlTraceAggregator = new SqlTraceAggregator(dataTransportService, scheduler, processStatic, agentHealthReporter);

			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);

			_sqlTraceMaker = new SqlTraceMaker(_configurationService, _attributeService);

			// create TransactionTransformer
			_transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, _segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService, _transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker, _transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker, _sqlTraceAggregator, _sqlTraceMaker, _spanEventAggregator, _spanEventMaker, _agentTimerService);
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
			var transactionName = _transactionMetricNameMaker.GetTransactionMetricName(transaction.TransactionName);
			Mock.Arrange(() => _configuration.SlowSqlEnabled).Returns(true);
			Mock.Arrange(() => _configuration.SqlExplainPlanThreshold).Returns(TimeSpan.FromMilliseconds(100));

			var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
			var args = new object[] { transaction, transactionName };
			privateTransactionTransformer.CallMethod("Transform", args);

			var privateSqlTraceStatsInAggregator = new PrivateAccessor(_sqlTraceAggregator).GetField("_sqlTraceStats");
			var privateSqlTraceStatsCollection = (SqlTraceStatsCollection)privateSqlTraceStatsInAggregator;
			var tracesCount = ((IDictionary<Int64, SqlTraceWireModel>)privateSqlTraceStatsCollection.Collection).Count;
			Assert.AreEqual(tracesCount, 1);
		}

		[Test]
		public void SqlTracesCollectedMetricIsAccurate()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			Mock.Arrange(() => _configuration.SlowSqlEnabled).Returns(true);
			Mock.Arrange(() => _configuration.SqlExplainPlanThreshold).Returns(TimeSpan.FromMilliseconds(100));
			var segments = new List<Segment>();

			int nextId = 0;
			var txSegmentState = Mock.Create<ITransactionSegmentState>();
			Mock.Arrange(() => txSegmentState.ParentSegmentId()).Returns(() =>
				nextId==0?(int?)null:nextId);
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
			var transactionName = _transactionMetricNameMaker.GetTransactionMetricName(transaction.TransactionName);

			var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
			var args = new object[] { transaction, transactionName };
			privateTransactionTransformer.CallMethod("Transform", args);

			String sqlTracesCollectedMetricName = "Supportability/SqlTraces/TotalSqlTracesCollected";
			Assert.IsTrue(generatedMetrics.TryGetValue(sqlTracesCollectedMetricName, out MetricDataWireModel data));
			Assert.AreEqual(3, data.Value0);
		}

		#region Helpers

		[NotNull]
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

using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Utilities;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class TransactionTransformerTests
	{
		[NotNull]
		private TransactionTransformer _transactionTransformer;

		[NotNull]
		private ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private ISegmentTreeMaker _segmentTreeMaker;

		[NotNull]
		private IMetricBuilder _metricBuilder;

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

		private ISpanEventAggregator _spanEventAggregator;

		private ISpanEventMaker _spanEventMaker;

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

		private ITransactionSegmentState _transactionSegmentState;
		private IAgentTimerService _agentTimerService;

		[SetUp]
		public void SetUp()
		{
			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => txName.IsWeb)))
				.Returns(new TransactionMetricName("WebTransaction", "TransactionName"));
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => !txName.IsWeb))).Returns(new TransactionMetricName("OtherTransaction", "TransactionName"));

			_transactionSegmentState = Mock.Create<ITransactionSegmentState>();

			Mock.Arrange(() => _transactionSegmentState.GetRelativeTime()).Returns(() => TimeSpan.Zero);

			_segmentTreeMaker = Mock.Create<ISegmentTreeMaker>();
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] {BuildNode()});

			_metricBuilder = GetSimpleMetricBuilder();
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
			_sqlTraceAggregator = Mock.Create<ISqlTraceAggregator>();
			_sqlTraceMaker = Mock.Create<ISqlTraceMaker>();
			_spanEventAggregator = Mock.Create<ISpanEventAggregator>();
			_spanEventMaker = Mock.Create<ISpanEventMaker>();
			_agentTimerService = Mock.Create<IAgentTimerService>();

			_transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, _segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService, _transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker, _transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker, _sqlTraceAggregator, _sqlTraceMaker, _spanEventAggregator, _spanEventMaker, _agentTimerService);
		}

		[NotNull]
		public IMetricBuilder GetSimpleMetricBuilder()
		{
			_metricNameService = Mock.Create<IMetricNameService>();
			Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<String>())).Returns<String>(name => name);
			return new MetricWireModel.MetricBuilder(_metricNameService);
		}

		#region Invalid/ignored transactions

		[Test]
		public void TransformerTransaction_DoesNotGenerateData_IfTransactionIsIgnored()
		{
			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("foo", "bar"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(),priority, Mock.Create<IDatabaseStatementParser>());
			transaction.Ignore();

			_transactionTransformer.Transform(transaction);

			AssertNoDataGenerated();
		}

		[Test]
		public void TransformerTransaction_DoesNotGenerateData_IfTransactionMetricNameIsIgnored()
		{
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(new TransactionMetricName("a", "b", true));
			var transaction = TestTransactions.CreateDefaultTransaction();

			_transactionTransformer.Transform(transaction);

			AssertNoDataGenerated();
		}

		[Test]
		public void TransformerTransaction_Throws_IfTransactionHasNoSegments()
		{
			var transaction = TestTransactions.CreateDefaultTransaction(addSegment: false);

			Assert.Throws<ArgumentException>(() => _transactionTransformer.Transform(transaction));

			AssertNoDataGenerated();
		}

		#endregion Invalid/ignored transactions

		#region Metrics

		[Test]
		public void SegmentTransformers_AreGivenAllSegments()
		{
			var node1 = GetNodeBuilder("seg1");
			var node2 = GetNodeBuilder("seg2");
			node1.Children.Add(node2);
			var node3 = GetNodeBuilder("seg3");
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { node1.Build(), node3.Build() });
			TransactionMetricStatsCollection txStats = null;
			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(stats => txStats = stats);

			var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });

			_transactionTransformer.Transform(transaction);

			var scopedStats = txStats.GetScopedForTesting();
			Assert.AreEqual(3, scopedStats.Count);
			var oneStat = scopedStats["DotNet/seg1"];
			Assert.AreEqual(1, oneStat.Value0);
			oneStat = scopedStats["DotNet/seg2"];
			Assert.AreEqual(1, oneStat.Value0);
			oneStat = scopedStats["DotNet/seg3"];
			Assert.AreEqual(1, oneStat.Value0);

			var unscopedStats = txStats.GetUnscopedForTesting();
			Assert.AreEqual(3, scopedStats.Count);
			oneStat = unscopedStats["DotNet/seg1"];
			Assert.AreEqual(1, oneStat.Value0);
			oneStat = unscopedStats["DotNet/seg2"];
			Assert.AreEqual(1, oneStat.Value0);
			oneStat = unscopedStats["DotNet/seg3"];
			Assert.AreEqual(1, oneStat.Value0);
		}

		[Test]
		public void SegmentTransformers_AreGivenCorrectTransactionName()
		{
			var node = BuildNode();
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { node });
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(new TransactionMetricName("WebTransaction", "TransactionName"));
			TransactionMetricStatsCollection txStats = null;
			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(stats => txStats = stats);

			var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node.Segment });

			_transactionTransformer.Transform(transaction);

			Assert.AreEqual("WebTransaction/TransactionName", txStats.GetTransactionName().PrefixedName);
		}

		[Test]
		public void SegmentTransformers_AreGivenCorrectChildDuration()
		{
			var node1 = GetNodeBuilder("seg1", new TimeSpan(), TimeSpan.FromSeconds(5)); // 0-5
			var node2 = GetNodeBuilder("seg2", new TimeSpan(), TimeSpan.FromSeconds(1)); // 0-1
			var node3 = GetNodeBuilder("seg3", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(999)); // 3-999, but should be capped at 3-5 by parent time
			node1.Segment.ChildFinished(node2.Segment);
			node1.Children.Add(node2);
			node1.Segment.ChildFinished(node3.Segment);
			node1.Children.Add(node3);
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { node1.Build() });

			TransactionMetricStatsCollection txStats = null;
			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(stats => txStats = stats);

			var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });
			
			_transactionTransformer.Transform(transaction);

		   var scopedStats = txStats.GetScopedForTesting();
			Assert.AreEqual(3, scopedStats.Count);
			var oneStat = scopedStats["DotNet/seg1"];
			Assert.AreEqual(1, oneStat.Value0);
			Assert.AreEqual(5, oneStat.Value1);
			Assert.AreEqual(0, oneStat.Value2);
			oneStat = scopedStats["DotNet/seg2"];
			Assert.AreEqual(1, oneStat.Value0);
			Assert.AreEqual(1, oneStat.Value1);
			Assert.AreEqual(1, oneStat.Value2);
			oneStat = scopedStats["DotNet/seg3"];
			Assert.AreEqual(1, oneStat.Value0);
			Assert.AreEqual(999, oneStat.Value1);
			Assert.AreEqual(999, oneStat.Value2);
		}

		public enum TestMode { DTDisabled, DTEnabled , PayloadDisabled, PayloadEnabled, SpanDisabled, SpanEnabled,  CatDisabled, CatEnabled, WebTransaction, OtherTransaction, WithError, NoError };
		[Test]
		[Combinatorial]
		public void TransactionRollupMetrics(
			[Values(TestMode.DTDisabled, TestMode.DTEnabled)]TestMode dtMode, 
			[Values(TestMode.PayloadDisabled, TestMode.PayloadEnabled)]TestMode payloadMode, 
			[Values(TestMode.SpanDisabled, TestMode.SpanEnabled)]TestMode spanMode, 
			[Values(TestMode.CatDisabled, TestMode.CatEnabled)]TestMode catMode, 
			[Values(TestMode.WebTransaction, TestMode.OtherTransaction)]TestMode webOrOtherMode, 
			[Values(TestMode.WithError, TestMode.NoError)]TestMode errorMode)
		{
			var distributedTraceEnabled = dtMode == TestMode.DTEnabled;
			var hasIncomingDistributedTracePayload = payloadMode == TestMode.PayloadEnabled;
			var spanEventsEnabled = spanMode == TestMode.SpanEnabled;
			var catEnabled = catMode == TestMode.CatEnabled;
			var isWebTransaction = webOrOtherMode == TestMode.WebTransaction;
			var isError = errorMode == TestMode.WithError;

			var actualMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(distributedTraceEnabled);
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(spanEventsEnabled);
			Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(catEnabled);

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>()))
				.DoInstead<TransactionMetricStatsCollection>(txStats => actualMetrics = txStats.GetUnscopedForTesting());

			var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction, statusCode: (isError)? 503 : 200);
			if (distributedTraceEnabled)
			{
				//make this a distributed trace participant
				transaction.TransactionMetadata.HasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
			}

			_transactionTransformer.Transform(transaction);

			var transactionType = isWebTransaction ? "Web" : "Other";

			//Because we mock the segment tree builder, that creates a root node with the name MyMockedRootNode.
			var expectedMetrics = new List<string>
			{
				"DotNet/MyMockedRootNode"
			};

			expectedMetrics.AddRange(new[]
			{
				transactionType + "Transaction/TransactionName",
				transactionType + "TransactionTotalTime",
				transactionType + "TransactionTotalTime/TransactionName"
			});

			if (isWebTransaction)
			{
				expectedMetrics.AddRange(new[]
				{
					"WebTransaction", "HttpDispatcher", "ApdexAll", "Apdex", "Apdex/TransactionName"
				});
			}
			else
			{
				expectedMetrics.AddRange(new[]
				{
					"OtherTransaction/all"
				});
			}

			if (isError)
			{
				expectedMetrics.AddRange(new[]
				{
					"Errors/all",
					"Errors/all"+transactionType,
					"Errors/" + transactionType+ "Transaction/TransactionName",
				});

				if (distributedTraceEnabled)
				{
					expectedMetrics.AddRange(new[]
					{
						"ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/all",
						"ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/all" + transactionType
					});
				}
			}

			if (_configuration.DistributedTracingEnabled)
			{
				expectedMetrics.AddRange(new[]
				{
					"DurationByCaller/Unknown/Unknown/Unknown/Unknown/all",
					"DurationByCaller/Unknown/Unknown/Unknown/Unknown/all" +transactionType
				});

				if (transaction.TransactionMetadata.HasIncomingDistributedTracePayload)
				{
					expectedMetrics.AddRange(new[]
					{
						"TransportDuration/Unknown/Unknown/Unknown/Unknown/all",
						"TransportDuration/Unknown/Unknown/Unknown/Unknown/all"+transactionType
					});
				}
			}

			Assert.That(actualMetrics.Keys, Is.EquivalentTo(expectedMetrics));
			foreach (var actual in actualMetrics)
			{
#warning Spec-Check! in the case of a transaction with an error, should the Apdex, ApdexAll, and Apdex/TransactionName metrics have a value of zero
				if (!isError || !(actual.Key == "ApdexAll" || actual.Key == "Apdex" || actual.Key == "Apdex/TransactionName"))
				{
					Assert.That(actual.Value.Value0, Is.EqualTo(1), actual.Key);
				}
			}
		}

		[Test]
		public void TransactionTotalTimeRollupMetricIsGeneratedOther()
		{
			var node1 = GetNodeBuilder(new TimeSpan(), TimeSpan.FromSeconds(5)); // 0-5
			var node2 = GetNodeBuilder(new TimeSpan(), TimeSpan.FromSeconds(3)); // 0-3
			var node3 = GetNodeBuilder(new TimeSpan(), TimeSpan.FromSeconds(3)); // 0-3
			node1.Children.Add(node2);
			node1.Segment.ChildFinished(node2.Segment);
			node1.Children.Add(node3);
			node1.Segment.ChildFinished(node3.Segment);

			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { node1.Build() });

			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction:false, segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });
			_transactionTransformer.Transform(transaction);

			//check the total time metrics
			String[] unscoped = new String[] {
				"OtherTransactionTotalTime", "OtherTransactionTotalTime/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.TryGetValue(current, out MetricDataWireModel data));
				Assert.AreEqual(1, data.Value0);
				Assert.AreEqual(6, data.Value1);
			}

		}

		[Test]
		public void TransactionTotalTimeRollupMetricIsGeneratedWeb(
			[Values(true, false)] bool distributedTraceEnabled,
			[Values(false, true)] bool hasIncomingDistributedTracePayload)
		{
			var node1 = GetNodeBuilder(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(7)); // 0-8
			var node2 = GetNodeBuilder(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)); // 0-3
			var node3 = GetNodeBuilder(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)); // 3-6
			node1.Children.Add(node2);
			node1.Segment.ChildFinished(node2.Segment);
			node1.Children.Add(node3);
			node1.Segment.ChildFinished(node3.Segment);
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { node1.Build() });

			Assert.AreEqual(1, node1.Segment.ExclusiveDurationOrZero.TotalSeconds);
			Assert.AreEqual(3, node2.Segment.ExclusiveDurationOrZero.TotalSeconds);
			Assert.AreEqual(3, node3.Segment.ExclusiveDurationOrZero.TotalSeconds);

			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(distributedTraceEnabled);

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction: true, segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });

			//make this a distributed trace participant
			if (distributedTraceEnabled)
			{
				transaction.TransactionMetadata.HasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
			}

			_transactionTransformer.Transform(transaction);

			var expected = 9;
			if (_configuration.DistributedTracingEnabled)
			{
				expected += 2;
				if (transaction.TransactionMetadata.HasIncomingDistributedTracePayload)
				{
					expected += 2;
				}
			}

			Assert.AreEqual(expected, generatedMetrics.Count);
			Assert.IsTrue(generatedMetrics.ContainsKey("DotNet/MyOtherMockedRootNode"));
			//check the total time metrics
			var unscoped = new[] {
				"WebTransactionTotalTime", "WebTransactionTotalTime/TransactionName"};

			foreach (var current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.TryGetValue(current, out MetricDataWireModel data));
				Assert.AreEqual(1, data.Value0);
				Assert.AreEqual(7, data.Value1);
			}

		}

		private static void AddDummySegment(Transaction transaction)
		{
			transaction.Add(SimpleSegmentDataTests.createSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, null, Enumerable.Empty<KeyValuePair<string, object>>(), "", false));
		}

		[Test]
		public void QueueTimeMetricIsGenerated_IfQueueTimeIsNotNull()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("foo", "bar"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
			AddDummySegment(transaction);

			_transactionTransformer.Transform(transaction);

			//check for webfrontend queue time (and a few others). This is not the entire list of unscoped.
			String[] unscoped = new String[] {
				"WebFrontend/QueueTime", "HttpDispatcher", "WebTransaction",
			"WebTransactionTotalTime"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.ContainsKey(current));
				var data = generatedMetrics[current];
				Assert.AreEqual(1, data.Value0);
			}
		}

		[Test]
		public void ApdexRollupMetricIsGenerated_IfApdexTIsNotNullAndIsNotErrorTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<String>())).Returns(TimeSpan.FromSeconds(1));
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>())).Returns(null as ErrorTraceWireModel);

			var transaction = TestTransactions.CreateDefaultTransaction();
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"ApdexAll", "Apdex", "Apdex/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.ContainsKey(current));
				var data = generatedMetrics[current];
				//satisfying
				Assert.AreEqual(1, data.Value0);
				// 3 and 4 are total time
				Assert.AreEqual(1, data.Value3);
				Assert.AreEqual(1, data.Value4);
			}
		}

		[Test]
		public void ApdexMetricsUseResponseTimeForWebTransactions()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<String>())).Returns(TimeSpan.FromSeconds(1));
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>())).Returns(null as ErrorTraceWireModel);

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsWebTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(10))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			//Apdex value based on the response time should be satisfying
			NrAssert.Multiple(
					() => Assert.AreEqual(1, generatedMetrics["Apdex/TransactionName"].Value0),
					() => Assert.AreEqual(1, generatedMetrics["Apdex"].Value0),
					() => Assert.AreEqual(1, generatedMetrics["ApdexAll"].Value0)
				);
		}

		[Test]
		public void ApdexMetricsUseDurationForOtherTransactions()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<String>())).Returns(TimeSpan.FromSeconds(1));
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>())).Returns(null as ErrorTraceWireModel);

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsOtherTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(10))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			//Apdex value based on the duration should be frustrating
			NrAssert.Multiple(
					() => Assert.AreEqual(1, generatedMetrics["ApdexOther/Transaction/TransactionName"].Value2),
					() => Assert.AreEqual(1, generatedMetrics["ApdexOther"].Value2),
					() => Assert.AreEqual(1, generatedMetrics["ApdexAll"].Value2),
					//We should not see apdex metrics that are for web transactions
					() => CollectionAssert.IsNotSubsetOf(new[] { "Apdex/TransactionName", "Apdex" }, generatedMetrics.Keys)
				);
		}

		[Test]
		public void FrustratedApdexRollupMetricIsGenerated_IfApdexTIsNotNullAndIsErrorTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<String>())).Returns(TimeSpan.FromSeconds(1));
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>())).Returns(GetError());

			var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404);
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"ApdexAll", "Apdex", "Apdex/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.ContainsKey(current));
				var data = generatedMetrics[current];
				//satisfying
				Assert.AreEqual(0, data.Value0);
				//tolerating
				Assert.AreEqual(0, data.Value1);
				// frustration
				Assert.AreEqual(1, data.Value2);
			}
		}

		[Test]
		public void FrustratedApdexRollupMetricIsNotGenerated_IfApdexTIsNullAndIsErrorTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<String>())).Returns((TimeSpan?)null);
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>())).Returns(GetError());

			var transaction = TestTransactions.CreateDefaultTransaction(false);
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"ApdexAll", "Apdex", "Apdex/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsFalse(generatedMetrics.ContainsKey(current));
			}
		}

		[Test]
		public void ErrorsAllMetricIsGenerated_IfIsErrorTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()))
				.Returns(GetError());

			var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404);
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"Errors/all", "Errors/allWeb", "Errors/WebTransaction/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.ContainsKey(current), "Metric is not contained: " + current);
				Assert.AreEqual(1, generatedMetrics[current].Value0);
			}
		}

		[Test]
		public void ErrorsAllMetricIsGenerated_OtherTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()))
				.Returns(GetError());

			var transaction = TestTransactions.CreateDefaultTransaction(false, statusCode: 404);
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsTrue(generatedMetrics.ContainsKey(current), "Metric is not contained: " + current);
				Assert.AreEqual(1, generatedMetrics[current].Value0);
			}
		}

		[Test]
		public void ErrorsAllMetricIsNotGenerated_IfIsNotErrorTransaction()
		{
			var generatedMetrics = new MetricStatsDictionary<String, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()))
			   .Returns(null as ErrorTraceWireModel);

			var transaction = TestTransactions.CreateDefaultTransaction(false);
			_transactionTransformer.Transform(transaction);

			String[] unscoped = new String[] {
				"Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};
			foreach (String current in unscoped)
			{
				Assert.IsFalse(generatedMetrics.ContainsKey(current), "Metric is contained: " + current);
			}
		}

		[Test]
		public void ClientApplicationMetricIsGenerated_IfReferringCrossProcessIdIsNotNull()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsWebTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(3))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.WithCrossApplicationData(crossApplicationReferrerProcessId: "123#456", crossApplicationResponseTimeInSeconds: 0.8f)
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			NrAssert.Multiple(
					() => Assert.AreEqual(1, generatedMetrics["ClientApplication/123#456/all"].Value0),
					() => Assert.AreEqual(0.8f, generatedMetrics["ClientApplication/123#456/all"].Value1),
					() => Assert.AreEqual(1, generatedMetrics["HttpDispatcher"].Value0),
					() => Assert.AreEqual(1, generatedMetrics["HttpDispatcher"].Value1),
					() => Assert.AreEqual(1, generatedMetrics["WebTransaction"].Value0),
					() => Assert.AreEqual(1, generatedMetrics["WebTransaction"].Value1)
				);
		}

		[Test]
		public void ClientApplicationMetricIsNotGenerated_IfReferringCrossProcessIdIsNull()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsWebTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(3))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			CollectionAssert.DoesNotContain(generatedMetrics.Keys, "ClientApplication/123#456/all");
		}

		[Test]
		public void TransactionMetricsUseResponseTimeForWebTransactions()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsWebTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(3))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			NrAssert.Multiple(
					() => Assert.AreEqual(1, generatedMetrics["WebTransaction/TransactionName"].Value1),
					() => Assert.AreEqual(1, generatedMetrics["WebTransaction"].Value1),
					() => Assert.AreEqual(1, generatedMetrics["HttpDispatcher"].Value1)
				);
		}

		[Test]
		public void TransactionMetricsUseDurationForOtherTransactions()
		{
			var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

			var immutableTransaction = new ImmutableTransactionBuilder()
				.IsOtherTransaction("category", "name")
				.WithDuration(TimeSpan.FromSeconds(3))
				.WithResponseTime(TimeSpan.FromSeconds(1))
				.Build();
			var transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

			_transactionTransformer.Transform(transaction);

			NrAssert.Multiple(
					() => Assert.AreEqual(3, generatedMetrics["OtherTransaction/TransactionName"].Value1),
					() => Assert.AreEqual(3, generatedMetrics["OtherTransaction/all"].Value1),
					() => CollectionAssert.DoesNotContain(generatedMetrics.Keys, "HttpDispatcher")
				);
		}

		#endregion Metrics
		/*


		[Test]
		public void ClientApplicationMetricIsNotGenerated_IfReferringCrossProcessIdIsNull()
		{
			var unexpectedMetric = Mock.Create<MetricWireModel>();
			var generatedMetrics = new List<MetricWireModel>();
			Mock.Arrange(() => _metricBuilder.TryBuildClientApplicationMetric(Arg.IsAny<String>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan>())).Returns(unexpectedMetric);
			Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(metric => generatedMetrics.Add(metric));

			var transaction = TestTransactions.CreateDefaultTransaction(referrerCrossProcessId: null);
			_transactionTransformer.Transform(transaction);

			Assert.IsFalse(generatedMetrics.Contains(unexpectedMetric));
		}

		#endregion Metrics

	*/
		#region Transaction Traces

		[Test]
		public void TransformSendsCorrectParametersToTraceMaker()
		{
			// ARRANGE
			var expectedAttributes = Mock.Create<Attributes>();
			var expectedSegmentTreeNodes = new List<ImmutableSegmentTreeNode> {BuildNode()};
			var expectedTransactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<ErrorData>(), Arg.IsAny<TransactionMetricStatsCollection>()))
				.Returns(expectedAttributes);
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(expectedSegmentTreeNodes);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(expectedTransactionMetricName);

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			AddDummySegment(transaction);

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(_transactionTraceMaker.GetTransactionTrace(transaction.ConvertToImmutableTransaction(), expectedSegmentTreeNodes, expectedTransactionMetricName, expectedAttributes));
		}

		[Test]
		public void TransactionTraceIsSentToAggregator()
		{
			var transaction = TestTransactions.CreateDefaultTransaction();
			_transactionTransformer.Transform(transaction);

			Mock.Assert(() => _transactionTraceAggregator.Collect(Arg.IsAny<TransactionTraceWireModelComponents>()));
		}

		[Test]
		public void TransactionTraceIsNotSentToAggregatorWhenTraceIsDisabled()
		{
			Mock.Arrange(() => _configuration.TransactionTracerEnabled).Returns(false);

			var transaction = TestTransactions.CreateDefaultTransaction();
			_transactionTransformer.Transform(transaction);

			Mock.Assert(() => _transactionTraceAggregator.Collect(Arg.IsAny<TransactionTraceWireModelComponents>()), Occurs.Never());
		}

		#endregion Transaction Traces

		#region Transaction Events

		[Test]
		public void TransformSendsCorrectParametersToEventMaker()
		{
			// ARRANGE
			var expectedAttributes = Mock.Create<Attributes>();
			Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<ErrorData>(), Arg.IsAny<TransactionMetricStatsCollection>()))
				.Returns(expectedAttributes);

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			AddDummySegment(transaction);

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(_transactionEventMaker.GetTransactionEvent(transaction.ConvertToImmutableTransaction(), expectedAttributes));
		}

		[Test]
		public void TransactionEventIsSentToAggregator()
		{
			// ARRANGE
			var transactionEvent = Mock.Create<TransactionEventWireModel>();
			Mock.Arrange(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>())).Returns(transactionEvent);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _transactionEventAggregator.Collect(transactionEvent));
		}

		[Test]
		public void TransactionEvent_IsNotCreatedOrSentToAggregator_IfTransactionEventsEnabledIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(false);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _transactionEventAggregator.Collect(Arg.IsAny<TransactionEventWireModel>()), Occurs.Never());
			Mock.Assert(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>()), Occurs.Never());
		}

		[Test]
		public void TransactionEvent_IsNotCreatedOrSentToAggregator_IfTransactionEventsTransactionsEnabledIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.TransactionEventsTransactionsEnabled).Returns(false);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _transactionEventAggregator.Collect(Arg.IsAny<TransactionEventWireModel>()), Occurs.Never());
			Mock.Assert(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>()), Occurs.Never());
		}

		#endregion Transaction Events

		#region Span Events

		[Test]
		public void TransformSendsCorrectParametersToSpanEventMaker()
		{
			// ARRANGE
			var transactionName = "transactionName";
			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", transactionName), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			AddDummySegment(transaction);

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(_spanEventMaker.GetSpanEvents(transaction.ConvertToImmutableTransaction(), transactionName));
		}

		[Test]
		public void SpanEventIsSentToAggregator()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);
			var transaction = TestTransactions.CreateDefaultTransaction(sampled: true);
			var spanEvents = _spanEventMaker.GetSpanEvents(transaction.ConvertToImmutableTransaction(), "transactionName");

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _spanEventAggregator.Collect(spanEvents));
		}

		[Test]
		public void SpanEvent_IsNotCreatedOrSentToAggregator_IfSpanEventsEnabledIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _spanEventAggregator.Collect(Arg.IsAny<List<SpanEventWireModel>>()), Occurs.Never());
			Mock.Assert(() => _spanEventMaker.GetSpanEvents(Arg.IsAny<ImmutableTransaction>(), "transactionName"), Occurs.Never());
		}

		#endregion

		#region Error Traces

		[Test]
		public void TransformSendsCorrectParametersToErrorTraceMaker()
		{
			// ARRANGE
			var expectedAttributes = Mock.Create<Attributes>();
			var expectedTransactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<ErrorData>(), Arg.IsAny<TransactionMetricStatsCollection>()))
				.Returns(expectedAttributes);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(expectedTransactionMetricName);
			var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404, transactionCategory: "transactionCategory", transactionName: "transactionName");

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), expectedAttributes, expectedTransactionMetricName, Arg.IsAny<ErrorData>()));
		}

		[Test]
		public void ErrorTraceIsSentToAggregator()
		{
			var errorTraceWireModel = Mock.Create<ErrorTraceWireModel>();
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()))
				.Returns(errorTraceWireModel);
			var transaction = TestTransactions.CreateDefaultTransaction(uri: "http://www.newrelic.com/test?param=value", statusCode: 404);

			var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
			var args = new object[] { transaction };
			privateTransactionTransformer.CallMethod("Transform", args);

			Mock.Assert(() => _errorTraceAggregator.Collect(errorTraceWireModel));
		}

		[Test]
		public void ErrorTraceIsNotSentToAggregatorWhenErrorTraceIsNull()
		{
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()))
				.Returns(null as ErrorTraceWireModel);
			var transaction = TestTransactions.CreateDefaultTransaction();
			_transactionTransformer.Transform(transaction);
			Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never());
		}

		[Test]
		public void ErrorTrace_IsNotCreatedOrSentToAggregator_IfErrorCollectorEnabledIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			AddDummySegment(transaction);

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<ErrorData>()), Occurs.Never());
			Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never());
		}

		#endregion Error Traces

		#region Error Events

		[Test]
		public void ErrorEventIsSentToAggregator()
		{
			// ARRANGE
			var errorEvent = Mock.Create<ErrorEventWireModel>();
			Mock.Arrange(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ErrorData>(), Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>())).Returns(errorEvent);
			var transaction = TestTransactions.CreateDefaultTransaction(uri: "http://www.newrelic.com/test?param=value", statusCode: 404);

			// ACT
			var privateTransactionTransformer = new PrivateAccessor(_transactionTransformer);
			var args = new object[] { transaction };
			privateTransactionTransformer.CallMethod("Transform", args);

			// ASSERT
			Mock.Assert(() => _errorEventAggregator.Collect(errorEvent));
		}

		[Test]
		public void ErrorEvent_IsNotCreatedOrSentToAggregator_IfErrorCollectorEnabledIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>()), Occurs.Never());
			Mock.Assert(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ErrorData>(), Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>()), Occurs.Never());
		}

		[Test]
		public void ErrorEvent_IsNotCreatedOrSentToAggregator_IfErrorCollectorCaptureEventsIsFalse()
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.ErrorCollectorCaptureEvents).Returns(false);
			var transaction = TestTransactions.CreateDefaultTransaction();

			// ACT
			_transactionTransformer.Transform(transaction);

			// ASSERT
			Mock.Assert(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>()), Occurs.Never());
			Mock.Assert(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ErrorData>(), Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<Attributes>()), Occurs.Never());
		}

		#endregion Error Events

		#region Helpers

		private void AssertNoDataGenerated()
		{
			NrAssert.Multiple(
				() => Mock.Assert(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>()), Occurs.Never()),
				() => Mock.Assert(() => _transactionTraceAggregator.Collect(Arg.IsAny<TransactionTraceWireModelComponents>()), Occurs.Never()),
				() => Mock.Assert(() => _transactionEventAggregator.Collect(Arg.IsAny<TransactionEventWireModel>()), Occurs.Never()),
				() => Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never()),
				() => Mock.Assert(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>()), Occurs.Never()),
				() => Mock.Assert(() => _sqlTraceAggregator.Collect(Arg.IsAny<SqlTraceStatsCollection>()), Occurs.Never())
			);
		}

		[NotNull]
		private ImmutableSegmentTreeNode BuildNode(TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
		{
			return new SegmentTreeNodeBuilder(
				GetSegment("MyMockedRootNode", duration.TotalSeconds, startTime)).
				Build();
		}

		[NotNull]
		private SegmentTreeNodeBuilder GetNodeBuilder(TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
		{
			return new SegmentTreeNodeBuilder(
				GetSegment("MyOtherMockedRootNode", duration.TotalSeconds, startTime));
		}

		[NotNull]
		private SegmentTreeNodeBuilder GetNodeBuilder(String name, TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
		{
			return new SegmentTreeNodeBuilder(
				GetSegment(name, duration.TotalSeconds, startTime));
		}

		[NotNull]
		private Segment GetSegment([NotNull] String name)
		{
			var builder = new TypedSegment<SimpleSegmentData>(_transactionSegmentState, new MethodCallData("foo", "bar", 1), new SimpleSegmentData(name));
			builder.End();
			return builder;
		}

		public TypedSegment<SimpleSegmentData> GetSegment([NotNull] String name, double duration, TimeSpan start = new TimeSpan())
		{
			return new TypedSegment<SimpleSegmentData>(start, TimeSpan.FromSeconds(duration), GetSegment(name));
		}

		[NotNull]
		public static IConfiguration GetDefaultConfiguration()
		{
			return TestTransactions.GetDefaultConfiguration();
		}

		[NotNull]
		private static ErrorTraceWireModel GetError()
		{
			var attributes = new Dictionary<string, object>();
			var stackTrace = new List<String>();
			var errorTraceAttributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attributes, attributes, attributes, stackTrace);
			return new ErrorTraceWireModel(DateTime.Now, "path", "message", "exceptionClassName", errorTraceAttributes, "guid");
		}
		
		#endregion Helpers
	}
}


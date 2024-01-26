// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class TransactionTransformerTests
    {
        private TransactionTransformer _transactionTransformer;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;

        private ISegmentTreeMaker _segmentTreeMaker;

        private IMetricBuilder _metricBuilder;

        private IMetricNameService _metricNameService;

        private IMetricAggregator _metricAggregator;

        private IConfigurationService _configurationService;

        private IConfiguration _configuration;

        private IDatabaseService _databaseService;

        private ITransactionTraceAggregator _transactionTraceAggregator;

        private ITransactionTraceMaker _transactionTraceMaker;

        private ITransactionEventAggregator _transactionEventAggregator;

        private ITransactionEventMaker _transactionEventMaker;

        private ISpanEventAggregator _spanEventAggregator;

        private ISpanEventAggregatorInfiniteTracing _spanEventAggregatorInfiniteTracing;

        private ISpanEventMaker _spanEventMaker;

        private ITransactionAttributeMaker _transactionAttributeMaker;

        private IErrorTraceAggregator _errorTraceAggregator;

        private IErrorTraceMaker _errorTraceMaker;

        private IErrorEventAggregator _errorEventAggregator;

        private IErrorEventMaker _errorEventMaker;

        private ISqlTraceAggregator _sqlTraceAggregator;

        private ISqlTraceMaker _sqlTraceMaker;

        private ITransactionSegmentState _transactionSegmentState;
        private IAgentTimerService _agentTimerService;
        private IErrorService _errorService;
        private IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;
        private ILogEventAggregator _logEventAggregator;

        private Action _harvestAction;
        private TimeSpan? _harvestCycle;

        [SetUp]
        public void SetUp()
        {
            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => txName.IsWeb)))
                .Returns(new TransactionMetricName("WebTransaction", "TransactionName"));
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => !txName.IsWeb))).Returns(new TransactionMetricName("OtherTransaction", "TransactionName"));

            _transactionSegmentState = TransactionSegmentStateHelpers.GetItransactionSegmentState();

            Mock.Arrange(() => _transactionSegmentState.GetRelativeTime()).Returns(() => TimeSpan.Zero);

            _segmentTreeMaker = Mock.Create<ISegmentTreeMaker>();
            Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
                .Returns(new[] { BuildNode() });

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
            _databaseService = Mock.Create<IDatabaseService>();
            _spanEventAggregator = Mock.Create<ISpanEventAggregator>();
            _spanEventAggregatorInfiniteTracing = Mock.Create<ISpanEventAggregatorInfiniteTracing>();
            _spanEventMaker = Mock.Create<ISpanEventMaker>();
            _agentTimerService = Mock.Create<IAgentTimerService>();
            _errorService = new ErrorService(_configurationService);
            _distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _logEventAggregator = new LogEventAggregator(Mock.Create<IDataTransportService>(), scheduler, Mock.Create<IProcessStatic>(), Mock.Create<IAgentHealthReporter>());


            _transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, _segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService, _transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker, _transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker, _sqlTraceAggregator, _sqlTraceMaker, _spanEventAggregator, _spanEventMaker, _agentTimerService, Mock.Create<IAdaptiveSampler>(), _errorService, _spanEventAggregatorInfiniteTracing, _logEventAggregator);
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
            _databaseService.Dispose();
            _logEventAggregator.Dispose();
            _metricNameService.Dispose();
            _sqlTraceAggregator.Dispose();
        }

        private IMetricBuilder GetSimpleMetricBuilder()
        {
            _metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            return new MetricWireModel.MetricBuilder(_metricNameService);
        }

        #region Invalid/ignored transactions

        [Test]
        public void TransformerTransaction_DoesNotGenerateData_IfTransactionIsIgnored()
        {
            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("foo", "bar"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
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
            Assert.That(scopedStats, Has.Count.EqualTo(3));
            var oneStat = scopedStats["DotNet/seg1"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));
            oneStat = scopedStats["DotNet/seg2"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));
            oneStat = scopedStats["DotNet/seg3"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));

            var unscopedStats = txStats.GetUnscopedForTesting();
            Assert.That(scopedStats, Has.Count.EqualTo(3));
            oneStat = unscopedStats["DotNet/seg1"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));
            oneStat = unscopedStats["DotNet/seg2"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));
            oneStat = unscopedStats["DotNet/seg3"];
            Assert.That(oneStat.Value0, Is.EqualTo(1));
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

            Assert.That(txStats.GetTransactionName().PrefixedName, Is.EqualTo("WebTransaction/TransactionName"));
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
            Assert.That(scopedStats, Has.Count.EqualTo(3));
            var oneStat = scopedStats["DotNet/seg1"];
            Assert.Multiple(() =>
            {
                Assert.That(oneStat.Value0, Is.EqualTo(1));
                Assert.That(oneStat.Value1, Is.EqualTo(5));
                Assert.That(oneStat.Value2, Is.EqualTo(0));
            });
            oneStat = scopedStats["DotNet/seg2"];
            Assert.Multiple(() =>
            {
                Assert.That(oneStat.Value0, Is.EqualTo(1));
                Assert.That(oneStat.Value1, Is.EqualTo(1));
                Assert.That(oneStat.Value2, Is.EqualTo(1));
            });
            oneStat = scopedStats["DotNet/seg3"];
            Assert.Multiple(() =>
            {
                Assert.That(oneStat.Value0, Is.EqualTo(1));
                Assert.That(oneStat.Value1, Is.EqualTo(999));
                Assert.That(oneStat.Value2, Is.EqualTo(999));
            });
        }

        public enum TestMode { DTDisabled, DTEnabled, PayloadDisabled, PayloadEnabled, SpanDisabled, SpanEnabled, CatDisabled, CatEnabled, WebTransaction, OtherTransaction, WithError, NoError };
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

            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(distributedTraceEnabled);
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(spanEventsEnabled);
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(catEnabled);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>()))
                .DoInstead<TransactionMetricStatsCollection>(txStats => actualMetrics = txStats.GetUnscopedForTesting());

            var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction, statusCode: (isError) ? 503 : 200);

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

                if (transaction.TracingState != null)
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

            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

            var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction: false, segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });
            _transactionTransformer.Transform(transaction);

            //check the total time metrics
            string[] unscoped = new string[] {
                "OtherTransactionTotalTime", "OtherTransactionTotalTime/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.TryGetValue(current, out MetricDataWireModel data), Is.True);
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(6));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(1));
                Assert.That(node2.Segment.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(3));
                Assert.That(node3.Segment.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(3));
            });

            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(distributedTraceEnabled);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

            var transaction = TestTransactions.CreateDefaultTransaction(isWebTransaction: true, segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment });

            _transactionTransformer.Transform(transaction);

            var expected = 9;
            if (_configuration.DistributedTracingEnabled)
            {
                expected += 2;
                if (transaction.TracingState != null)
                {
                    expected += 2;
                }
            }

            Assert.That(generatedMetrics, Has.Count.EqualTo(expected));
            Assert.That(generatedMetrics.ContainsKey("DotNet/MyOtherMockedRootNode"), Is.True);
            //check the total time metrics
            var unscoped = new[] {
                "WebTransactionTotalTime", "WebTransactionTotalTime/TransactionName"};

            foreach (var current in unscoped)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.TryGetValue(current, out MetricDataWireModel data), Is.True);
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(7));
                });
            }

        }

        private static void AddDummySegment(Transaction transaction)
        {
            transaction.Add(SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, new MethodCallData("typeName", "methodName", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "", false));
        }

        [Test]
        public void QueueTimeMetricIsGenerated_IfQueueTimeIsNotNull()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("foo", "bar"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            AddDummySegment(transaction);

            _transactionTransformer.Transform(transaction);

            //check for webfrontend queue time (and a few others). This is not the entire list of unscoped.
            string[] unscoped = new string[] {
                "WebFrontend/QueueTime", "HttpDispatcher", "WebTransaction",
            "WebTransactionTotalTime"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.True);
                var data = generatedMetrics[current];
                Assert.That(data.Value0, Is.EqualTo(1));
            }
        }

        [Test]
        public void ApdexRollupMetricIsGenerated_IfApdexTIsNotNullAndIsNotErrorTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(1));
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(null as ErrorTraceWireModel);

            var transaction = TestTransactions.CreateDefaultTransaction();
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "ApdexAll", "Apdex", "Apdex/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.True);
                var data = generatedMetrics[current];
                Assert.Multiple(() =>
                {
                    //satisfying
                    Assert.That(data.Value0, Is.EqualTo(1));
                    // 3 and 4 are total time
                    Assert.That(data.Value3, Is.EqualTo(1));
                    Assert.That(data.Value4, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public void ApdexMetricsUseResponseTimeForWebTransactions()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(1));
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(null as ErrorTraceWireModel);

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
                    () => Assert.That(generatedMetrics["Apdex/TransactionName"].Value0, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["Apdex"].Value0, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["ApdexAll"].Value0, Is.EqualTo(1))
                );
        }

        [Test]
        public void ApdexMetricsUseDurationForOtherTransactions()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(1));
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(null as ErrorTraceWireModel);

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
                    () => Assert.That(generatedMetrics["ApdexOther/Transaction/TransactionName"].Value2, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["ApdexOther"].Value2, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["ApdexAll"].Value2, Is.EqualTo(1)),
                    //We should not see apdex metrics that are for web transactions
                    () => Assert.That(new[] { "Apdex/TransactionName", "Apdex" }, Is.Not.SubsetOf(generatedMetrics.Keys))
                );
        }

        [Test]
        public void FrustratedApdexRollupMetricIsGenerated_IfApdexTIsNotNullAndIsErrorTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(1));
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(GetError());

            var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "ApdexAll", "Apdex", "Apdex/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.True);
                var data = generatedMetrics[current];
                Assert.Multiple(() =>
                {
                    //satisfying
                    Assert.That(data.Value0, Is.EqualTo(0));
                    //tolerating
                    Assert.That(data.Value1, Is.EqualTo(0));
                    // frustration
                    Assert.That(data.Value2, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public void FrustratedApdexRollupMetricForErrors_NotAffectedByErrorCollectorEnabled([Values(true, false)]bool errorCollectorEnabled)
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(errorCollectorEnabled);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(1));
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(GetError());

            var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "ApdexAll", "Apdex", "Apdex/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.True);
                var data = generatedMetrics[current];
                Assert.Multiple(() =>
                {
                    //satisfying
                    Assert.That(data.Value0, Is.EqualTo(0));
                    //tolerating
                    Assert.That(data.Value1, Is.EqualTo(0));
                    // frustration
                    Assert.That(data.Value2, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public void FrustratedApdexRollupMetricIsNotGenerated_IfApdexTIsNullAndIsErrorTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns((TimeSpan?)null);
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(GetError());

            var transaction = TestTransactions.CreateDefaultTransaction(false);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "ApdexAll", "Apdex", "Apdex/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.False);
            }
        }

        [Test]
        public void ErrorMetricsAreGenerated_IfErrorCollectionIsEnabled()
        {
            var actualMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>()))
                .DoInstead<TransactionMetricStatsCollection>(txStats => actualMetrics = txStats.GetUnscopedForTesting());

            var transaction = TestTransactions.CreateDefaultTransaction(true, statusCode: 503);

            _transactionTransformer.Transform(transaction);

            var expectedMetrics = new List<string>
            {
                "Errors/all",
                "Errors/allWeb",
                "Errors/WebTransaction/TransactionName",
                "ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/all",
                "ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/allWeb"
            };

            Assert.That(expectedMetrics, Is.SubsetOf(actualMetrics.Keys));
            foreach (var expected in expectedMetrics)
            {
                Assert.That(actualMetrics[expected].Value0, Is.EqualTo(1), $"Expected {expected} metric to have a count value of 1");
            }
        }

        [Test]
        public void ErrorMetricsAreNotGenerated_IfErrorCollectionIsNotEnabled()
        {
            var actualMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>()))
                .DoInstead<TransactionMetricStatsCollection>(txStats => actualMetrics = txStats.GetUnscopedForTesting());

            var transaction = TestTransactions.CreateDefaultTransaction(true, statusCode: 503);

            _transactionTransformer.Transform(transaction);

            var unexpectedMetrics = new List<string>
            {
                "Errors/all",
                "Errors/allWeb",
                "Errors/WebTransaction/TransactionName",
                "ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/all",
                "ErrorsByCaller/Unknown/Unknown/Unknown/Unknown/allWeb"
            };

            Assert.That(unexpectedMetrics, Is.Not.SubsetOf(actualMetrics.Keys));
        }

        [Test]
        public void ErrorsAllMetricIsGenerated_IfIsErrorTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()))
                .Returns(GetError());

            var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "Errors/all", "Errors/allWeb", "Errors/WebTransaction/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.ContainsKey(current), Is.True, "Metric is not contained: " + current);
                    Assert.That(generatedMetrics[current].Value0, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public void ErrorsAllMetricIsGenerated_OtherTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()))
                .Returns(GetError());

            var transaction = TestTransactions.CreateDefaultTransaction(false, statusCode: 404);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.ContainsKey(current), Is.True, "Metric is not contained: " + current);
                    Assert.That(generatedMetrics[current].Value0, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public void ErrorsAllMetricIsNotGenerated_IfIsNotErrorTransaction()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()))
               .Returns(null as ErrorTraceWireModel);

            var transaction = TestTransactions.CreateDefaultTransaction(false);
            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.False, "Metric is contained: " + current);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ErrorMetrics_ExpectedErrorClasses(bool expectForError)
        {

            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();
            var errorEvents = new List<ErrorEventWireModel>();
            var errorTraces = new List<ErrorTraceWireModel>();

            _errorTraceMaker = new ErrorTraceMaker(_configurationService, _attribDefSvc, _agentTimerService);
            _errorEventMaker = new ErrorEventMaker(_attribDefSvc, _configurationService, _agentTimerService);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>())).DoInstead<ErrorEventWireModel>(errorEvent => errorEvents.Add(errorEvent));
            Mock.Arrange(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>())).DoInstead<ErrorTraceWireModel>(errorTrace => errorTraces.Add(errorTrace));
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(2));

            if (expectForError)
            {
                Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
                Mock.Arrange(() => _configuration.ExpectedErrorsConfiguration).Returns(new Dictionary<string, IEnumerable<string>>()
                {
                    { "System.IO.IOException", Enumerable.Empty<string>()}
                });
            }

            var transaction = TestTransactions.CreateDefaultTransaction(false, configurationService: _configurationService, exception: new IOException());

            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};

            // When an error is expected, unscoped error metrics should not be generated.
            foreach (string current in unscoped)
            {
                if (expectForError)
                {
                    Assert.That(generatedMetrics.ContainsKey(current), Is.False, "Metric should not contain: " + current);

                }
                else
                {
                    Assert.That(generatedMetrics.ContainsKey(current), Is.True, "Metric should contain: " + current);
                }
            }

            // When an error is expected, ErrorsExpected/all metric is generated, frustrating score apdex metric should not be generated. 
            if (expectForError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.ContainsKey("ErrorsExpected/all"), Is.True);
                    Assert.That(generatedMetrics["ApdexOther/Transaction/TransactionName"].Value0, Is.EqualTo(1)); //sastisfying apdex
                    Assert.That(generatedMetrics["ApdexOther"].Value0, Is.EqualTo(1)); //sastisfying apdex
                    Assert.That(generatedMetrics["ApdexAll"].Value0, Is.EqualTo(1)); //sastisfying
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(generatedMetrics.ContainsKey("ErrorsExpected/all"), Is.False);
                    Assert.That(generatedMetrics["ApdexOther/Transaction/TransactionName"].Value2, Is.EqualTo(1)); //frustrating apdex
                    Assert.That(generatedMetrics["ApdexOther"].Value2, Is.EqualTo(1)); //frustrating apdex
                    Assert.That(generatedMetrics["ApdexAll"].Value2, Is.EqualTo(1)); //frustrating apdex
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(errorEvents, Is.Not.Empty, "Expect error events.");
                Assert.That(errorTraces, Is.Not.Empty, "Expect error traces.");
            });
        }

        [Test]
        public void IgnoredErrors_Override_ExpectedErrors()
        {
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();
            var errorEvents = new List<ErrorEventWireModel>();
            var errorTraces = new List<ErrorTraceWireModel>();

            _errorTraceMaker = new ErrorTraceMaker(_configurationService, _attribDefSvc, _agentTimerService);
            _errorEventMaker = new ErrorEventMaker(_attribDefSvc, _configurationService, _agentTimerService);

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());
            Mock.Arrange(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>())).DoInstead<ErrorEventWireModel>(errorEvent => errorEvents.Add(errorEvent));
            Mock.Arrange(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>())).DoInstead<ErrorTraceWireModel>(errorTrace => errorTraces.Add(errorTrace));
            Mock.Arrange(() => _metricNameService.TryGetApdex_t(Arg.IsAny<string>())).Returns(TimeSpan.FromSeconds(2));

            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ExpectedErrorsConfiguration).Returns(new Dictionary<string, IEnumerable<string>>()
            {
                { "System.IO.IOException", Enumerable.Empty<string>()}
            });
            Mock.Arrange(() => _configuration.IgnoreErrorsConfiguration).Returns(new Dictionary<string, IEnumerable<string>>
            {
                { "System.IO.IOException", Enumerable.Empty<string>()}
            });

            var transaction = TestTransactions.CreateDefaultTransaction(false, configurationService: _configurationService, exception: new IOException());

            _transactionTransformer.Transform(transaction);

            string[] unscoped = new string[] {
                "Errors/all", "Errors/allOther", "Errors/OtherTransaction/TransactionName"};

            // When an error is ignored, unscoped error metrics should not be generated.
            foreach (string current in unscoped)
            {
                Assert.That(generatedMetrics.ContainsKey(current), Is.False, "Metric should not contain: " + current);
            }

            Assert.Multiple(() =>
            {
                // When an error is ignored, ErrorsExpected/all metric is generated, frustrating score apdex metric should not be generated. 
                Assert.That(generatedMetrics.ContainsKey("ErrorsExpected/all"), Is.False);
                Assert.That(generatedMetrics["ApdexOther/Transaction/TransactionName"].Value0, Is.EqualTo(1)); //sastisfying apdex
                Assert.That(generatedMetrics["ApdexOther"].Value0, Is.EqualTo(1)); //sastisfying apdex
                Assert.That(generatedMetrics["ApdexAll"].Value0, Is.EqualTo(1)); //sastisfying apdex

                Assert.That(errorEvents, Is.Empty, "Expect no error events.");
                Assert.That(errorTraces, Is.Empty, "Expect no error traces.");
            });
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
                    () => Assert.That(generatedMetrics["ClientApplication/123#456/all"].Value0, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["ClientApplication/123#456/all"].Value1, Is.EqualTo(0.8f)),
                    () => Assert.That(generatedMetrics["HttpDispatcher"].Value0, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["HttpDispatcher"].Value1, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["WebTransaction"].Value0, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["WebTransaction"].Value1, Is.EqualTo(1))
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

            Assert.That(generatedMetrics.Keys, Has.No.Member("ClientApplication/123#456/all"));
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
                    () => Assert.That(generatedMetrics["WebTransaction/TransactionName"].Value1, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["WebTransaction"].Value1, Is.EqualTo(1)),
                    () => Assert.That(generatedMetrics["HttpDispatcher"].Value1, Is.EqualTo(1))
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
                    () => Assert.That(generatedMetrics["OtherTransaction/TransactionName"].Value1, Is.EqualTo(3)),
                    () => Assert.That(generatedMetrics["OtherTransaction/all"].Value1, Is.EqualTo(3)),
                    () => Assert.That(generatedMetrics.Keys, Has.No.Member("HttpDispatcher"))
                );
        }

        [Test]
        public void TransportDurationMetricIsGenerated_IfDistributedTraceHeadersReceived()
        {
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
            var generatedMetrics = new MetricStatsDictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<TransactionMetricStatsCollection>())).DoInstead<TransactionMetricStatsCollection>(txStats => generatedMetrics = txStats.GetUnscopedForTesting());

            var immutableTransaction = new ImmutableTransactionBuilder()
                .WithStartTime(DateTime.UtcNow)
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(3))
                .WithResponseTime(TimeSpan.FromSeconds(1))
                .WithW3CTracing("guid", "parentId", null)
                .Build();

            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.ConvertToImmutableTransaction()).Returns(immutableTransaction);

            _transactionTransformer.Transform(transaction);

            // note: metric strings below include default values from .WithW3CTracing; those values can/should be parameterized if other tests start using .WithW3CTracing
            NrAssert.Multiple(
                () => Assert.That(generatedMetrics.ContainsKey("TransportDuration/App/accountId/appId/Kafka/all"), Is.True, $"Missing TransportDuration metric TransportDuration/App/accountId/appId/Kafka/all"),
                () => Assert.That(generatedMetrics.ContainsKey("TransportDuration/App/accountId/appId/Kafka/allWeb"), Is.True, $"Missing TransportDuration metric TransportDuration/App/accountId/appId/Kafka/allWeb"),
                () => Assert.That(generatedMetrics["TransportDuration/App/accountId/appId/Kafka/all"].Value1, Is.GreaterThan(0)),
                () => Assert.That(generatedMetrics["TransportDuration/App/accountId/appId/Kafka/allWeb"].Value1, Is.GreaterThan(0))
                );
        }

        #endregion Metrics

        #region Transaction Traces

        [Test]
        public void TransformSendsCorrectParametersToTraceMaker()
        {
            // ARRANGE
            var expectedAttributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);
            var expectedSegmentTreeNodes = new List<ImmutableSegmentTreeNode> { BuildNode() };
            var expectedTransactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TransactionMetricStatsCollection>()))
                .Returns(expectedAttributes);
            Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
                .Returns(expectedSegmentTreeNodes);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
                .Returns(expectedTransactionMetricName);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
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
            var expectedAttributes = new AttributeValueCollection(AttributeDestinations.TransactionEvent);
            Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TransactionMetricStatsCollection>()))
                .Returns(expectedAttributes);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
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
            Mock.Arrange(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>())).Returns(transactionEvent);
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
            Mock.Assert(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>()), Occurs.Never());
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
            Mock.Assert(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>()), Occurs.Never());
        }

        #endregion Transaction Events

        #region Span Events

        [Test]
        public void RecordsSupportabilityInfiniteTracingSeenAndDroppedWhenNoCapacity()
        {
            const int testValHasCapacity = 17;
            const int testValNoCapacity = 63;


            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceEnabled).Returns(true);
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceAvailable).Returns(true);

            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.HasCapacity(testValHasCapacity)).Returns(true);
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.HasCapacity(testValNoCapacity)).Returns(false);

            var actualSpansSeen = 0;
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.RecordSeenSpans(Arg.IsAny<int>()))
                .DoInstead<int>((countSpansSeen) =>
                {
                    Interlocked.Add(ref actualSpansSeen, countSpansSeen);
                });

            var actualSpansDropped = 0;
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.RecordDroppedSpans(Arg.IsAny<int>()))
                .DoInstead<int>((countSpansDropped) =>
                {
                    Interlocked.Add(ref actualSpansDropped, countSpansDropped);
                });

            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.Collect(Arg.IsAny<IEnumerable<ISpanEventWireModel>>()))
                .DoInstead<IEnumerable<ISpanEventWireModel>>((spans) =>
                {
                    Interlocked.Add(ref actualSpansSeen, spans.Count());
                });

            Mock.Arrange(() => _spanEventMaker.GetSpanEvents(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<string>(), Arg.IsAny<IAttributeValueCollection>()))
                .Returns<ImmutableTransaction, string, IAttributeValueCollection>((trx, trxName, trxAttribValues) =>
                {
                    var result = new List<ISpanEventWireModel>();
                    result.Add(new SpanAttributeValueCollection()); //accounting for root span
                    result.AddRange(trx.Segments.Select(x => x.GetAttributeValues()));

                    return result;
                });

            var transactionName = "transactionName";
            var priority = 0.5f;
            var trxHasCapacity = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", transactionName), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            for (var i = 0; i < testValHasCapacity - 1; i++)
            {
                AddDummySegment(trxHasCapacity);
            }

            _transactionTransformer.Transform(trxHasCapacity);

            var trxNoCapacity = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", transactionName), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            for (var i = 0; i < testValNoCapacity - 1; i++)
            {
                AddDummySegment(trxNoCapacity);
            }

            _transactionTransformer.Transform(trxNoCapacity);

            NrAssert.Multiple
            (
                () => Assert.That(actualSpansSeen, Is.EqualTo(testValHasCapacity + testValNoCapacity)),
                () => Assert.That(actualSpansDropped, Is.EqualTo(testValNoCapacity))
            );
        }

        [Test]
        public void TransformSendsCorrectParametersToSpanEventMaker()
        {
            // ARRANGE
            var transactionName = "transactionName";
            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", transactionName), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            AddDummySegment(transaction);

            // ACT
            _transactionTransformer.Transform(transaction);

            // ASSERT
            Mock.Assert(_spanEventMaker.GetSpanEvents(transaction.ConvertToImmutableTransaction(), transactionName, Arg.IsAny<IAttributeValueCollection>()));
        }

        [Test]
        public void SpanEvents_AreCreated_WhichAggregator
        (
            [Values(true, false)] bool infiniteTracingEnabled,
            [Values(true, false)] bool infiniteTracingServiceAvailable,
            [Values(true, false)] bool traditionalTracingEnabled,
            [Values(true, false)] bool transactionIsSampled
        )
        {
            // ARRANGE
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceEnabled).Returns(infiniteTracingEnabled);
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceAvailable).Returns(infiniteTracingServiceAvailable);
            Mock.Arrange(() => _spanEventAggregator.IsServiceEnabled).Returns(traditionalTracingEnabled);
            Mock.Arrange(() => _spanEventAggregator.IsServiceAvailable).Returns(traditionalTracingEnabled);
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.HasCapacity(Arg.IsAny<int>())).Returns(true);

            var actualCallCountInfiniteTracingAggregator = 0;
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.Collect(Arg.IsAny<IEnumerable<ISpanEventWireModel>>()))
                .DoInstead(() => actualCallCountInfiniteTracingAggregator++);

            var actualCallCountTraditionalTracingAggregator = 0;
            Mock.Arrange(() => _spanEventAggregator.Collect(Arg.IsAny<IEnumerable<ISpanEventWireModel>>()))
                .DoInstead(() => actualCallCountTraditionalTracingAggregator++);


            var expect8TCalled = infiniteTracingServiceAvailable && infiniteTracingEnabled;
            var expectTraditionalTracingCalled = traditionalTracingEnabled && transactionIsSampled && !expect8TCalled;
            var expectedGetSpanEventsCalled = expect8TCalled || expectTraditionalTracingCalled;

            var transaction = TestTransactions.CreateDefaultTransaction(sampled: transactionIsSampled);

            // ACT
            _transactionTransformer.Transform(transaction);

            NrAssert.Multiple
            (
                () => Assert.That(actualCallCountTraditionalTracingAggregator, Is.EqualTo(expectTraditionalTracingCalled ? 1 : 0), $"Traditional Tracing should {(expectTraditionalTracingCalled ? "" : "NOT ")}have been called"),
                () => Assert.That(actualCallCountInfiniteTracingAggregator, Is.EqualTo(expect8TCalled ? 1 : 0), $"Infinite Tracing should {(expect8TCalled ? "" : "NOT ")}have been called"),
                () => Mock.Assert(() => _spanEventMaker.GetSpanEvents(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<string>(), Arg.IsAny<IAttributeValueCollection>()), expectedGetSpanEventsCalled ? Occurs.Once() : Occurs.Never(), $"GetSpanEvents should {(expectedGetSpanEventsCalled ? "" : "NOT ")} have been called.")
            );
        }

        #endregion

        #region Error Traces

        [Test]
        public void TransformSendsCorrectParametersToErrorTraceMaker()
        {
            // ARRANGE
            var expectedAttributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var expectedTransactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            Mock.Arrange(() => _transactionAttributeMaker.GetAttributes(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TransactionMetricStatsCollection>()))
                .Returns(expectedAttributes);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
                .Returns(expectedTransactionMetricName);
            var transaction = TestTransactions.CreateDefaultTransaction(statusCode: 404, transactionCategory: "transactionCategory", transactionName: "transactionName");

            // ACT
            _transactionTransformer.Transform(transaction);

            // ASSERT
            Mock.Assert(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), expectedAttributes, expectedTransactionMetricName));
        }

        [Test]
        public void ErrorTraceIsSentToAggregator()
        {
            var errorTraceWireModel = Mock.Create<ErrorTraceWireModel>();
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()))
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
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()))
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
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            AddDummySegment(transaction);

            // ACT
            _transactionTransformer.Transform(transaction);

            // ASSERT
            Mock.Assert(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>()), Occurs.Never());
            Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never());
        }

        #endregion Error Traces

        #region Error Events

        [Test]
        public void ErrorEventIsSentToAggregator()
        {
            // ARRANGE
            var errorEvent = Mock.Create<ErrorEventWireModel>();
            Mock.Arrange(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>())).Returns(errorEvent);
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
            Mock.Assert(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>()), Occurs.Never());
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
            Mock.Assert(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>()), Occurs.Never());
        }

        #endregion Error Events

        #region Log Events

        [Test]
        public void PrioritizeAndCollectLogEvents_PriorityMatchesTransaction()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid", new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } });

            var transaction = TestTransactions.CreateDefaultTransaction();
            transaction.AddLogEvent(logEvent);

            _transactionTransformer.Transform(transaction);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessorL = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessorL.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var handledLogEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(handledLogEvent, Is.Not.Null);
            });
            Assert.That(handledLogEvent.Priority, Is.EqualTo(transaction.Priority), $"{transaction.Priority} vs {handledLogEvent.Priority}");
        }

        [Test]
        public void CannotAddLogEventsToTransaction_AfterTransform()
        {
            var logEvent = new LogEventWireModel(1, "message1", "info", "spanid", "traceid", new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } });
            var transaction = TestTransactions.CreateDefaultTransaction();

            _transactionTransformer.Transform(transaction);

            Assert.That(transaction.AddLogEvent(logEvent), Is.False);
        }

        #endregion

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

        private ImmutableSegmentTreeNode BuildNode(TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
        {
            return new SegmentTreeNodeBuilder(
                GetSegment("MyMockedRootNode", duration.TotalSeconds, startTime)).
                Build();
        }

        private SegmentTreeNodeBuilder GetNodeBuilder(TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
        {
            return new SegmentTreeNodeBuilder(
                GetSegment("MyOtherMockedRootNode", duration.TotalSeconds, startTime));
        }

        private SegmentTreeNodeBuilder GetNodeBuilder(string name, TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
        {
            return new SegmentTreeNodeBuilder(
                GetSegment(name, duration.TotalSeconds, startTime));
        }

        private Segment GetSegment(string name)
        {
            var builder = new Segment(_transactionSegmentState, new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new SimpleSegmentData(name));
            builder.End();
            return builder;
        }

        private Segment GetSegment(string name, double duration, TimeSpan start = new TimeSpan())
        {
            return new Segment(start, TimeSpan.FromSeconds(duration), GetSegment(name), null);
        }

        private static IConfiguration GetDefaultConfiguration()
        {
            return TestTransactions.GetDefaultConfiguration();
        }

        private static ErrorTraceWireModel GetError()
        {
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var stackTrace = new List<string>();
            var errorTraceAttributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attributes, stackTrace);
            return new ErrorTraceWireModel(DateTime.Now, "path", "message", "exceptionClassName", errorTraceAttributes, "guid");
        }

        #endregion Helpers
    }
}


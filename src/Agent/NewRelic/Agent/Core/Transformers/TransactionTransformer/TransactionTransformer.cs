using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionTransformer
    {
        void Transform(ITransaction transaction);
    }

    public class TransactionTransformer : ITransactionTransformer
    {
        private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;
        private readonly ISegmentTreeMaker _segmentTreeMaker;
        private readonly IMetricNameService _metricNameService;
        private readonly IMetricAggregator _metricAggregator;
        private readonly IConfigurationService _configurationService;
        private readonly ITransactionTraceAggregator _transactionTraceAggregator;
        private readonly ITransactionTraceMaker _transactionTraceMaker;
        private readonly ITransactionEventAggregator _transactionEventAggregator;
        private readonly ITransactionEventMaker _transactionEventMaker;
        private readonly ITransactionAttributeMaker _transactionAttributeMaker;
        private readonly IErrorTraceAggregator _errorTraceAggregator;
        private readonly IErrorTraceMaker _errorTraceMaker;
        private readonly IErrorEventAggregator _errorEventAggregator;
        private readonly IErrorEventMaker _errorEventMaker;
        private readonly ISqlTraceAggregator _sqlTraceAggregator;
        private readonly ISqlTraceMaker _sqlTraceMaker;

        public TransactionTransformer(ITransactionMetricNameMaker transactionMetricNameMaker, ISegmentTreeMaker segmentTreeMaker, IMetricNameService metricNameService, IMetricAggregator metricAggregator, IConfigurationService configurationService, ITransactionTraceAggregator transactionTraceAggregator, ITransactionTraceMaker transactionTraceMaker, ITransactionEventAggregator transactionEventAggregator, ITransactionEventMaker transactionEventMaker, ITransactionAttributeMaker transactionAttributeMaker, IErrorTraceAggregator errorTraceAggregator, IErrorTraceMaker errorTraceMaker, IErrorEventAggregator errorEventAggregator, IErrorEventMaker errorEventMaker, ISqlTraceAggregator sqlTraceAggregator, ISqlTraceMaker sqlTraceMaker)
        {
            _transactionMetricNameMaker = transactionMetricNameMaker;
            _segmentTreeMaker = segmentTreeMaker;
            _metricNameService = metricNameService;
            _metricAggregator = metricAggregator;
            _configurationService = configurationService;
            _transactionTraceAggregator = transactionTraceAggregator;
            _transactionTraceMaker = transactionTraceMaker;
            _transactionEventAggregator = transactionEventAggregator;
            _transactionEventMaker = transactionEventMaker;
            _transactionAttributeMaker = transactionAttributeMaker;
            _errorTraceAggregator = errorTraceAggregator;
            _errorTraceMaker = errorTraceMaker;
            _errorEventAggregator = errorEventAggregator;
            _errorEventMaker = errorEventMaker;
            _sqlTraceAggregator = sqlTraceAggregator;
            _sqlTraceMaker = sqlTraceMaker;
        }

        public void Transform(ITransaction transaction)
        {
            if (transaction.Ignored)
                return;
            Transform(transaction.ConvertToImmutableTransaction());
        }

        private void Transform(ImmutableTransaction immutableTransaction)
        {
            // Note: Metric names are normally handled internally by the IMetricBuilder. However, transactionMetricName is an exception because (sadly) it is used for more than just metrics. For example, transaction events need to use metric name, as does RUM and CAT.
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            if (transactionMetricName.ShouldIgnore)
            {
                Log.Finest($"Transaction \"{transactionMetricName}\" is being ignored due to metric naming rules");
                return;
            }

            if (!immutableTransaction.Segments.Any())
                throw new ArgumentException("Transaction does not have any segments");

            FinishSegments(immutableTransaction.Segments);

            TryGenerateExplainPlans(immutableTransaction.Segments);

            var totalTime = GetTotalExclusiveTime(immutableTransaction.Segments);
            var transactionApdexMetricName = MetricNames.GetTransactionApdex(transactionMetricName);
            var apdexT = GetApdexT(immutableTransaction, transactionMetricName.PrefixedName);

            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

            GenerateAndCollectSqlTrace(immutableTransaction, transactionMetricName, txStats);
            GenerateAndCollectMetrics(immutableTransaction, transactionMetricName, errorData.IsAnError, apdexT, transactionApdexMetricName, totalTime, txStats);

            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

            // Must generate errors first so other wire models get attribute updates
            if (errorData.IsAnError)
            {
                GenerateAndCollectErrorEventTracesAndEvents(immutableTransaction, attributes, transactionMetricName, errorData);
            }

            GenerateAndCollectTransactionEvent(immutableTransaction, attributes);

            GenerateAndCollectTransactionTrace(immutableTransaction, transactionMetricName, attributes);
        }

        private static void FinishSegments(IEnumerable<Segment> segments)
        {
            Stack<Segment> unfinishedSegments = new Stack<Segment>();
            foreach (var segment in segments)
            {
                if (segment.RelativeEndTime.HasValue == false)
                {
                    unfinishedSegments.Push(segment);
                }
            }

            // if we have to force any segments to finish, do it in the reverse order of their creation
            // to guarantee that children are finished before their parents
            foreach (var segment in unfinishedSegments)
            {
                // if the segment ended between being added to the unfinished list and now, this call to 
                // end will have no effect
                segment.ForceEnd();
                Log.FinestFormat("Force segment to finish for method {0}", segment.MethodCallData);
            }
        }

        private static TimeSpan GetTotalExclusiveTime(IEnumerable<Segment> segments)
        {
            long total = 0;

            foreach (var segment in segments)
            {
                total += segment.ExclusiveDurationOrZero.Ticks;
            }

            return new TimeSpan(total);
        }
        private ErrorTraceWireModel GenerateErrorTrace(ImmutableTransaction immutableTransaction, Attributes attributes, TransactionMetricName transactionMetricName, ErrorData errorData)
        {
            if (!_configurationService.Configuration.ErrorCollectorEnabled)
                return null;

            return _errorTraceMaker.GetErrorTrace(immutableTransaction, attributes, transactionMetricName, errorData);
        }

        private TimeSpan? GetApdexT(ImmutableTransaction immutableTransaction, string transactionApdexMetricName)
        {
            var apdexT = _metricNameService.TryGetApdex_t(transactionApdexMetricName);
            if (immutableTransaction.IsWebTransaction())
                apdexT = apdexT ?? _configurationService.Configuration.TransactionTraceApdexT;

            return apdexT;
        }

        private void GenerateAndCollectErrorEventTracesAndEvents(ImmutableTransaction immutableTransaction, Attributes attributes, TransactionMetricName transactionMetricName, ErrorData errorData)
        {
            var errorTrace = GenerateErrorTrace(immutableTransaction, attributes, transactionMetricName, errorData);
            if (errorTrace == null)
                return;

            _errorTraceAggregator.Collect(errorTrace);

            if (_configurationService.Configuration.ErrorCollectorCaptureEvents)
            {
                var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);
                _errorEventAggregator.Collect(errorEvent);
            }
        }

        private void GenerateAndCollectMetrics(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, bool isErrorTranasction, TimeSpan? apdexT, string transactionApdexMetricName, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
        {
            foreach (var segment in immutableTransaction.Segments)
            {
                GenerateSegmentMetrics(segment, txStats);
            }

            var isWebTransaction = immutableTransaction.IsWebTransaction();

            {
                // Response time is just EndTime minus StartTime
                MetricBuilder.TryBuildTransactionMetrics(isWebTransaction, immutableTransaction.Duration, txStats);

                // Total time is the total amount of time spent, even when work is happening parallel, which means it is the sum of all exclusive times.
                // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
                MetricBuilder.TryBuildTotalTimeMetrics(isWebTransaction, totalTime, txStats);

                // CPU time is the total time spent actually doing work rather than waiting. Basically, it's TotalTime minus TimeSpentWaiting.
                // Our agent does not yet the ability to calculate time spent waiting, so we cannot generate this metric.
                // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
                //_metricBuilder.TryBuildCpuTimeRollupMetric(isWebTransaction, immutableTransaction.Duration, txStats),
                //_metricBuilder.TryBuildCpuTimeMetric(transactionMetricName, immutableTransaction.Duration, txStats)
            };

            if (immutableTransaction.TransactionMetadata.QueueTime != null)
                MetricBuilder.TryBuildQueueTimeMetric(immutableTransaction.TransactionMetadata.QueueTime.Value, txStats);

            if (apdexT != null && !immutableTransaction.IgnoreApdex)
            {
                GetApdexMetrics(immutableTransaction, isErrorTranasction, apdexT.Value, transactionApdexMetricName, txStats);
            }

            if (isErrorTranasction)
            {
                MetricBuilder.TryBuildErrorsMetrics(isWebTransaction, txStats);
            }

            var referrerCrossProcessId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId;
            if (referrerCrossProcessId != null)
                MetricBuilder.TryBuildClientApplicationMetric(referrerCrossProcessId, immutableTransaction.Duration, immutableTransaction.Duration, txStats);

            _metricAggregator.Collect(txStats);
        }

        private void GenerateAndCollectTransactionEvent(ImmutableTransaction immutableTransaction, Attributes attributes)
        {
            if (!_configurationService.Configuration.TransactionEventsEnabled)
                return;

            if (!_configurationService.Configuration.TransactionEventsTransactionsEnabled)
                return;

            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);
            _transactionEventAggregator.Collect(transactionEvent);
        }

        private void GenerateAndCollectTransactionTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, Attributes attributes)
        {
            if (!_configurationService.Configuration.TransactionTracerEnabled)
                return;

            var traceComponents = new TransactionTraceWireModelComponents(
                            transactionMetricName,
                            immutableTransaction.Duration,
                            immutableTransaction.TransactionMetadata.IsSynthetics,
                            () => _transactionTraceMaker.GetTransactionTrace(immutableTransaction, _segmentTreeMaker.BuildSegmentTrees(immutableTransaction.Segments), transactionMetricName, attributes));

            _transactionTraceAggregator.Collect(traceComponents);
        }

        private void GenerateAndCollectSqlTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TransactionMetricStatsCollection txStats)
        {
            if (!_configurationService.Configuration.SlowSqlEnabled)
                return;

            var txSqlTrStats = new SqlTraceStatsCollection();

            immutableTransaction.Segments.OfType<TypedSegment<DatastoreSegmentData>>().Where(
                segment => segment.TypedData.CommandText != null &&
                segment.Duration >= _configurationService.Configuration.SqlExplainPlanThreshold)
                .ForEach(segment => AddSqlTraceStats(txSqlTrStats, _sqlTraceMaker.TryGetSqlTrace(immutableTransaction, transactionMetricName, segment)));

            if (txSqlTrStats.Collection.Count > 0)
            {
                _sqlTraceAggregator.Collect(txSqlTrStats);

                MetricBuilder.TryBuildSqlTracesCollectedMetric(txSqlTrStats.TracesCollected, txStats);
            }

        }

        private void TryGenerateExplainPlans(IEnumerable<Segment> segments)
        {
            if (!_configurationService.Configuration.SqlExplainPlansEnabled)
            {
                return;
            }
            try
            {
                using (new IgnoreWork())
                {
                    short count = 0;
                    var sqlExplainPlansMax = _configurationService.Configuration.SqlExplainPlansMax;
                    var threshold = _configurationService.Configuration.SqlExplainPlanThreshold;
                    var obfuscator = SqlObfuscator.GetSqlObfuscator(_configurationService.Configuration.TransactionTracerEnabled, _configurationService.Configuration.TransactionTracerRecordSql);
                    foreach (var segment in segments)
                    {
                        if (segment.Duration > threshold && segment is TypedSegment<DatastoreSegmentData>)
                        {
                            var datastoreSegmentData = ((TypedSegment<DatastoreSegmentData>)segment).TypedData;
                            if (datastoreSegmentData.DoExplainPlanCondition?.Invoke() == true)
                            {
                                datastoreSegmentData.ExecuteExplainPlan(obfuscator);
                                if (++count >= sqlExplainPlansMax)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.DebugFormat("Exception occurred while generating explain plan: {0}", exception);
            }
        }

        private void GenerateSegmentMetrics(Segment segment, TransactionMetricStatsCollection txStats)
        {
            segment.AddMetricStats(txStats, _configurationService);
        }
        private void GetApdexMetrics(ImmutableTransaction immutableTransaction, bool isErrorTranasction, TimeSpan apdexT, string transactionApdexMetricName, TransactionMetricStatsCollection txStats)
        {
            var isWebTransaction = immutableTransaction.IsWebTransaction();

            if (isErrorTranasction)
            {
                MetricBuilder.TryBuildFrustratedApdexMetrics(isWebTransaction, transactionApdexMetricName, txStats);
            }
            else
            {
                MetricBuilder.TryBuildApdexMetrics(transactionApdexMetricName, isWebTransaction, immutableTransaction.Duration, apdexT, txStats);
            }
        }

        private void AddSqlTraceStats(SqlTraceStatsCollection txSqlTrStats, SqlTraceWireModel model)
        {
            txSqlTrStats.Insert(model);
        }
    }
}

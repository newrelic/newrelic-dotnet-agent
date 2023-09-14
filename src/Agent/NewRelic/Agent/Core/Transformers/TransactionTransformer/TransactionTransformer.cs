// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionTransformer
    {
        void Transform(IInternalTransaction transaction);
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
        private readonly ISpanEventAggregator _spanEventAggregator;
        private readonly ISpanEventAggregatorInfiniteTracing _spanEventAggregatorInfiniteTracing;
        private readonly ISpanEventMaker _spanEventMaker;
        private readonly ITransactionAttributeMaker _transactionAttributeMaker;
        private readonly IErrorTraceAggregator _errorTraceAggregator;
        private readonly IErrorTraceMaker _errorTraceMaker;
        private readonly IErrorEventAggregator _errorEventAggregator;
        private readonly IErrorEventMaker _errorEventMaker;
        private readonly ISqlTraceAggregator _sqlTraceAggregator;
        private readonly ISqlTraceMaker _sqlTraceMaker;
        private readonly IAgentTimerService _agentTimerService;
        private readonly IAdaptiveSampler _adaptiveSampler;
        private readonly IErrorService _errorService;
        private readonly ILogEventAggregator _logEventAggregator;

        public TransactionTransformer(ITransactionMetricNameMaker transactionMetricNameMaker, ISegmentTreeMaker segmentTreeMaker, IMetricNameService metricNameService, IMetricAggregator metricAggregator, IConfigurationService configurationService, ITransactionTraceAggregator transactionTraceAggregator, ITransactionTraceMaker transactionTraceMaker, ITransactionEventAggregator transactionEventAggregator, ITransactionEventMaker transactionEventMaker, ITransactionAttributeMaker transactionAttributeMaker, IErrorTraceAggregator errorTraceAggregator, IErrorTraceMaker errorTraceMaker, IErrorEventAggregator errorEventAggregator, IErrorEventMaker errorEventMaker, ISqlTraceAggregator sqlTraceAggregator, ISqlTraceMaker sqlTraceMaker, ISpanEventAggregator spanEventAggregator, ISpanEventMaker spanEventMaker, IAgentTimerService agentTimerService,
            IAdaptiveSampler adaptiveSampler, IErrorService errorService, ISpanEventAggregatorInfiniteTracing spanEventAggregatorInfiniteTracing, ILogEventAggregator logEventAggregator)
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
            _spanEventAggregator = spanEventAggregator;
            _spanEventAggregatorInfiniteTracing = spanEventAggregatorInfiniteTracing;
            _spanEventMaker = spanEventMaker;
            _agentTimerService = agentTimerService;
            _adaptiveSampler = adaptiveSampler;
            _errorService = errorService;
            _logEventAggregator = logEventAggregator;
        }

        public void Transform(IInternalTransaction transaction)
        {
            if (transaction.Ignored)
            {
                return;
            }

            ComputeSampled(transaction);
            PrioritizeAndCollectLogEvents(transaction);
            
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            // Note: Metric names are normally handled internally by the IMetricBuilder. However, transactionMetricName is an exception because (sadly) it is used for more than just metrics. For example, transaction events need to use metric name, as does RUM and CAT.
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            if (transactionMetricName.ShouldIgnore)
            {
                Log.Finest("Transaction \"{0}\" is being ignored due to metric naming rules", transactionMetricName);
                return;
            }

            using (_agentTimerService.StartNew("Transform", transactionMetricName.PrefixedName))
            {
                Transform(immutableTransaction, transactionMetricName);
            }

            Log.Finest("Transaction {0} ({1}) transform completed.", transaction.Guid, transactionMetricName);
        }

        private void Transform(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName)
        {
            if (!immutableTransaction.Segments.Any())
                throw new ArgumentException("Transaction does not have any segments");

            FinishSegments(immutableTransaction.Segments);

            TryGenerateExplainPlans(immutableTransaction.Segments);

            var totalTime = GetTotalExclusiveTime(immutableTransaction.Segments);
            var transactionApdexMetricName = MetricNames.GetTransactionApdex(transactionMetricName);
            var apdexT = GetApdexT(immutableTransaction, transactionMetricName.PrefixedName);

            var txStats = new TransactionMetricStatsCollection(transactionMetricName);

            GenerateAndCollectSqlTrace(immutableTransaction, transactionMetricName, txStats);
            GenerateAndCollectMetrics(immutableTransaction, apdexT, transactionApdexMetricName, totalTime, txStats);

            // defer the creation of attributes until something asks for them.
            Func<IAttributeValueCollection> attributes = () => _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);
            attributes = attributes.Memoize();

            // Must generate errors first so other wire models get attribute updates
            if (immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
            {
                GenerateAndCollectErrorEventTracesAndEvents(immutableTransaction, attributes.Invoke(), transactionMetricName);
            }

            GenerateAndCollectTransactionEvent(immutableTransaction, attributes);

            GenerateAndCollectTransactionTrace(immutableTransaction, transactionMetricName, attributes);

            GenerateAndCollectSpanEvents(immutableTransaction, transactionMetricName.PrefixedName, attributes);
        }

        private static void FinishSegments(IEnumerable<Segment> segments)
        {
            Stack<Segment> unfinishedSegments = new Stack<Segment>();
            foreach (var segment in segments)
            {
                if (!segment.RelativeEndTime.HasValue)
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
                Log.Finest("Force segment to finish for method {0}", segment.MethodCallData);
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

        private ErrorTraceWireModel GenerateErrorTrace(ImmutableTransaction immutableTransaction, IAttributeValueCollection attributes, TransactionMetricName transactionMetricName)
        {
            if (!ErrorCollectionEnabled())
                return null;

            return _errorTraceMaker.GetErrorTrace(immutableTransaction, attributes, transactionMetricName);
        }

        private TimeSpan? GetApdexT(ImmutableTransaction immutableTransaction, string transactionApdexMetricName)
        {
            var apdexT = _metricNameService.TryGetApdex_t(transactionApdexMetricName);
            if (immutableTransaction.IsWebTransaction())
                apdexT = apdexT ?? _configurationService.Configuration.TransactionTraceApdexT;

            return apdexT;
        }

        private void GenerateAndCollectErrorEventTracesAndEvents(ImmutableTransaction immutableTransaction, IAttributeValueCollection attributes, TransactionMetricName transactionMetricName)
        {
            var errorTrace = GenerateErrorTrace(immutableTransaction, attributes, transactionMetricName);
            if (errorTrace == null)
                return;

            using (_agentTimerService.StartNew("CollectErrorTrace"))
            {
                _errorTraceAggregator.Collect(errorTrace);
            }

            if (_configurationService.Configuration.ErrorCollectorCaptureEvents)
            {
                var errorEvent = _errorEventMaker.GetErrorEvent(immutableTransaction, attributes);
                using (_agentTimerService.StartNew("CollectErrorEvent"))
                {
                    _errorEventAggregator.Collect(errorEvent);
                }
            }
        }

        private void GenerateAndCollectMetrics(ImmutableTransaction immutableTransaction, TimeSpan? apdexT, string transactionApdexMetricName, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
        {
            foreach (var segment in immutableTransaction.Segments)
            {
                GenerateSegmentMetrics(segment, txStats);
            }

            var isWebTransaction = immutableTransaction.IsWebTransaction();

            if (_configurationService.Configuration.DistributedTracingEnabled)
            {
                TimeSpan duration = default;
                string type = default;
                string account = default;
                string app = default;
                string transport = default;

                if (immutableTransaction.TracingState != null)
                {
                    duration = immutableTransaction.TracingState.TransportDuration;
                    type = EnumNameCache<DistributedTracingParentType>.GetName(immutableTransaction.TracingState.Type);
                    account = immutableTransaction.TracingState.AccountId;
                    app = immutableTransaction.TracingState.AppId;
                    transport = EnumNameCache<TransportType>.GetName(immutableTransaction.TracingState.TransportType);
                }

                MetricBuilder.TryBuildDistributedTraceDurationByCaller(type, account, app, transport, isWebTransaction,
                    immutableTransaction.Duration, txStats);

                if (immutableTransaction.TracingState != null)
                {
                    MetricBuilder.TryBuildDistributedTraceTransportDuration(type, account, app, transport, isWebTransaction, duration, txStats);
                }

                if (ErrorCollectionEnabled() && immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
                {
                    MetricBuilder.TryBuildDistributedTraceErrorsByCaller(type, account, app, transport, isWebTransaction, txStats);
                }
            }

            MetricBuilder.TryBuildTransactionMetrics(isWebTransaction, immutableTransaction.ResponseTimeOrDuration, txStats);

            // Total time is the total amount of time spent, even when work is happening parallel, which means it is the sum of all exclusive times.
            // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
            MetricBuilder.TryBuildTotalTimeMetrics(isWebTransaction, totalTime, txStats);

            // CPU time is the total time spent actually doing work rather than waiting. Basically, it's TotalTime minus TimeSpentWaiting.
            // Our agent does not yet the ability to calculate time spent waiting, so we cannot generate this metric.
            // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
            //_metricBuilder.TryBuildCpuTimeRollupMetric(isWebTransaction, immutableTransaction.Duration, txStats),
            //_metricBuilder.TryBuildCpuTimeMetric(transactionMetricName, immutableTransaction.Duration, txStats)

            if (immutableTransaction.TransactionMetadata.QueueTime != null)
                MetricBuilder.TryBuildQueueTimeMetric(immutableTransaction.TransactionMetadata.QueueTime.Value, txStats);

            if (apdexT != null && !immutableTransaction.IgnoreApdex)
            {
                GetApdexMetrics(immutableTransaction, apdexT.Value, transactionApdexMetricName, txStats);
            }

            if (ErrorCollectionEnabled() && immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
            {
                var isErrorExpected = immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.IsExpected;
                MetricBuilder.TryBuildErrorsMetrics(isWebTransaction, txStats, isErrorExpected);
            }

            var referrerCrossProcessId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId;
            if (referrerCrossProcessId != null)
            {
                var catResponseTime = TimeSpan.FromSeconds(immutableTransaction.TransactionMetadata.CrossApplicationResponseTimeInSeconds);
                MetricBuilder.TryBuildClientApplicationMetric(referrerCrossProcessId, catResponseTime, catResponseTime, txStats);
            }

            using (_agentTimerService.StartNew("CollectMetrics"))
            {
                _metricAggregator.Collect(txStats);
            }
        }

        private void GenerateAndCollectTransactionEvent(ImmutableTransaction immutableTransaction, Func<IAttributeValueCollection> attributes)
        {
            if (!_configurationService.Configuration.TransactionEventsEnabled)
                return;

            if (!_configurationService.Configuration.TransactionEventsTransactionsEnabled)
                return;

            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes.Invoke());
            using (_agentTimerService.StartNew("CollectTransactionEvent"))
            {
                _transactionEventAggregator.Collect(transactionEvent);
            }
        }

        private void GenerateAndCollectSpanEvents(ImmutableTransaction immutableTransaction, string transactionName, Func<IAttributeValueCollection> attributes)
        {
            var useInfiniteTracing = _spanEventAggregatorInfiniteTracing.IsServiceEnabled && _spanEventAggregatorInfiniteTracing.IsServiceAvailable;
            var useTraditionalTracing = !useInfiniteTracing && immutableTransaction.Sampled && _spanEventAggregator.IsServiceEnabled && _spanEventAggregator.IsServiceAvailable;

            if (!useInfiniteTracing && !useTraditionalTracing)
            {
                return;
            }

            var countProposedSpans = immutableTransaction.Segments.Count + 1;

            if (useInfiniteTracing && !_spanEventAggregatorInfiniteTracing.HasCapacity(countProposedSpans))
            {
                _spanEventAggregatorInfiniteTracing.RecordSeenSpans(countProposedSpans);
                _spanEventAggregatorInfiniteTracing.RecordDroppedSpans(countProposedSpans);
                return;
            }

            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, transactionName, attributes.Invoke());
            using (_agentTimerService.StartNew("CollectSpanEvents"))
            {
                if (useInfiniteTracing)
                {
                    _spanEventAggregatorInfiniteTracing.Collect(spanEvents);
                }
                else
                {
                    _spanEventAggregator.Collect(spanEvents);
                }
            }
        }

        private void GenerateAndCollectTransactionTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, Func<IAttributeValueCollection> attributes)
        {
            if (!_configurationService.Configuration.TransactionTracerEnabled)
            {
                return;
            }

            var traceComponents = new TransactionTraceWireModelComponents(
                            transactionMetricName,
                            immutableTransaction.Duration,
                            immutableTransaction.TransactionMetadata.IsSynthetics,
                            () => _transactionTraceMaker.GetTransactionTrace(immutableTransaction, _segmentTreeMaker.BuildSegmentTrees(immutableTransaction.Segments), transactionMetricName, attributes.Invoke()));

            using (_agentTimerService.StartNew("CollectTransactionTrace"))
            {
                _transactionTraceAggregator.Collect(traceComponents);
            }
        }

        private void GenerateAndCollectSqlTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TransactionMetricStatsCollection txStats)
        {
            if (!_configurationService.Configuration.SlowSqlEnabled)
            {
                return;
            }

            var txSqlTrStats = new SqlTraceStatsCollection();

            foreach (var segment in immutableTransaction.Segments.Where(s => s.Data is DatastoreSegmentData))
            {
                var datastoreSegmentData = (DatastoreSegmentData)segment.Data;
                if (datastoreSegmentData.CommandText != null &&
                     segment.Duration >= _configurationService.Configuration.SqlExplainPlanThreshold)
                {
                    AddSqlTraceStats(txSqlTrStats, _sqlTraceMaker.TryGetSqlTrace(immutableTransaction, transactionMetricName, segment));
                }
            }

            if (txSqlTrStats.Collection.Count > 0)
            {
                using (_agentTimerService.StartNew("CollectSqlTrace"))
                {
                    _sqlTraceAggregator.Collect(txSqlTrStats);
                }

                MetricBuilder.TryBuildSqlTracesCollectedMetric(txSqlTrStats.TracesCollected, txStats);
            }
        }

        private void TryGenerateExplainPlans(IEnumerable<Segment> segments)
        {
            // First, check if explainPlans are disabled and return if they are
            // If explainPlans are enabled, check if both TransactionTracer and SlowSql are disabled.  If they are, we don't need a plan, so return.
            if (!_configurationService.Configuration.SqlExplainPlansEnabled
                || (!_configurationService.Configuration.TransactionTracerEnabled && !_configurationService.Configuration.SlowSqlEnabled))
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
                    var obfuscator = SqlObfuscator.GetSqlObfuscator(_configurationService.Configuration.TransactionTracerRecordSql);
                    foreach (var segment in segments.Where(s => s.Data is DatastoreSegmentData))
                    {
                        if (segment.Duration > threshold)
                        {
                            var datastoreSegmentData = (DatastoreSegmentData)segment.Data;
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
                Log.Debug(exception, "Exception occurred while generating explain plan");
            }
        }

        private void GenerateSegmentMetrics(Segment segment, TransactionMetricStatsCollection txStats)
        {
            segment.AddMetricStats(txStats, _configurationService);
        }

        private void GetApdexMetrics(ImmutableTransaction immutableTransaction, TimeSpan apdexT, string transactionApdexMetricName, TransactionMetricStatsCollection txStats)
        {
            var isWebTransaction = immutableTransaction.IsWebTransaction();

            if (immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError
                && !immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.IsExpected)
            {
                MetricBuilder.TryBuildFrustratedApdexMetrics(isWebTransaction, transactionApdexMetricName, txStats);
            }
            else
            {
                MetricBuilder.TryBuildApdexMetrics(transactionApdexMetricName, isWebTransaction, immutableTransaction.ResponseTimeOrDuration, apdexT, txStats);
            }
        }

        private void AddSqlTraceStats(SqlTraceStatsCollection txSqlTrStats, SqlTraceWireModel model)
        {
            txSqlTrStats.Insert(model);
        }

        private void ComputeSampled(IInternalTransaction transaction)
        {
            if (_configurationService.Configuration.DistributedTracingEnabled)
            {
                transaction.SetSampled(_adaptiveSampler);
            }
        }

        private bool ErrorCollectionEnabled()
        {
            return _configurationService.Configuration.ErrorCollectorEnabled;
        }

        private void PrioritizeAndCollectLogEvents(IInternalTransaction transaction)
        {
            _logEventAggregator.CollectWithPriority(transaction.HarvestLogEvents(), transaction.Priority);
        }
    }
}
